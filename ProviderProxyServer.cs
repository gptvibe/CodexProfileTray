using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CodexProfileTray;

internal sealed class ProviderProxyServer : IDisposable
{
    public const int Port = 17345;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromMinutes(10)
    };

    private readonly ProviderSettingsStore _settingsStore;
    private readonly CancellationTokenSource _stop = new();
    private HttpListener? _listener;
    private Task? _runTask;

    public ProviderProxyServer(ProviderSettingsStore settingsStore)
    {
        _settingsStore = settingsStore;
    }

    public bool IsRunning => _listener?.IsListening == true;

    public static string GetProviderBaseUrl(string providerId)
    {
        return $"http://127.0.0.1:{Port}/providers/{Uri.EscapeDataString(providerId)}";
    }

    public static bool ShouldProxyProvider(string providerId)
    {
        return !providerId.Equals("openai", StringComparison.OrdinalIgnoreCase) &&
               !providerId.Equals("openai-api", StringComparison.OrdinalIgnoreCase);
    }

    public static bool TryGetProviderId(string? baseUrl, out string providerId)
    {
        providerId = string.Empty;
        if (string.IsNullOrWhiteSpace(baseUrl) ||
            !Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) ||
            !uri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
            uri.Port != Port)
        {
            return false;
        }

        var parts = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 || !parts[0].Equals("providers", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        providerId = Uri.UnescapeDataString(parts[1]);
        return !string.IsNullOrWhiteSpace(providerId);
    }

    public void Start()
    {
        if (IsRunning)
        {
            return;
        }

        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
        _listener.Start();
        _runTask = Task.Run(() => RunAsync(_stop.Token));
    }

    public void Dispose()
    {
        _stop.Cancel();
        try
        {
            _listener?.Stop();
            _listener?.Close();
            _runTask?.Wait(TimeSpan.FromSeconds(2));
        }
        catch
        {
            // Best effort during app shutdown.
        }
        finally
        {
            _stop.Dispose();
        }
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        if (_listener is null)
        {
            return;
        }

        while (!cancellationToken.IsCancellationRequested && _listener.IsListening)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (HttpListenerException)
            {
                break;
            }
            catch
            {
                continue;
            }

            _ = Task.Run(() => HandleAsync(context, cancellationToken), cancellationToken);
        }
    }

    private async Task HandleAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        try
        {
            var pathParts = context.Request.Url?.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries)
                ?? Array.Empty<string>();
            if (pathParts.Length < 3 || !pathParts[0].Equals("providers", StringComparison.OrdinalIgnoreCase))
            {
                await WriteJsonAsync(context, HttpStatusCode.NotFound, new { error = "Unknown proxy route." }, cancellationToken).ConfigureAwait(false);
                return;
            }

            var providerId = Uri.UnescapeDataString(pathParts[1]);
            var route = pathParts[2];
            if (context.Request.HttpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase) &&
                route.Equals("models", StringComparison.OrdinalIgnoreCase))
            {
                await HandleModelsAsync(context, providerId, cancellationToken).ConfigureAwait(false);
                return;
            }

            if (context.Request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase) &&
                route.Equals("responses", StringComparison.OrdinalIgnoreCase))
            {
                await HandleResponsesAsync(context, providerId, cancellationToken).ConfigureAwait(false);
                return;
            }

            await WriteJsonAsync(context, HttpStatusCode.NotFound, new { error = "Unknown proxy route." }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            if (context.Response.OutputStream.CanWrite)
            {
                await WriteJsonAsync(context, HttpStatusCode.InternalServerError, new { error = ex.Message }, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task HandleModelsAsync(HttpListenerContext context, string providerId, CancellationToken cancellationToken)
    {
        var settings = ResolveSettings(providerId);
        if (settings is null)
        {
            await WriteJsonAsync(context, HttpStatusCode.NotFound, new { error = $"Provider '{providerId}' is not configured." }, cancellationToken).ConfigureAwait(false);
            return;
        }

        var models = settings.Models.ToArray();
        var key = ResolveApiKey(settings);
        if (!string.IsNullOrWhiteSpace(key))
        {
            try
            {
                models = (await ModelFetcher.FetchAsync(settings.BaseUrl, key, cancellationToken).ConfigureAwait(false)).ToArray();
                if (models.Length > 0)
                {
                    settings.Models = models.ToList();
                    _settingsStore.Upsert(settings);
                }
            }
            catch
            {
                // Codex can continue with the saved model list when provider model refresh is unavailable.
            }
        }

        await WriteJsonAsync(context, HttpStatusCode.OK, new
        {
            @object = "list",
            data = models
                .Where(model => !string.IsNullOrWhiteSpace(model))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(model => new
                {
                    id = model,
                    @object = "model",
                    created = 0,
                    owned_by = settings.ProviderName
                })
                .ToArray()
        }, cancellationToken).ConfigureAwait(false);
    }

    private async Task HandleResponsesAsync(HttpListenerContext context, string providerId, CancellationToken cancellationToken)
    {
        var settings = ResolveSettings(providerId);
        if (settings is null)
        {
            await WriteJsonAsync(context, HttpStatusCode.NotFound, new { error = $"Provider '{providerId}' is not configured." }, cancellationToken).ConfigureAwait(false);
            return;
        }

        var key = ResolveApiKey(settings);
        if (string.IsNullOrWhiteSpace(key))
        {
            await WriteJsonAsync(context, HttpStatusCode.Unauthorized, new { error = $"No API key is saved for {settings.ProviderName}." }, cancellationToken).ConfigureAwait(false);
            return;
        }

        using var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding);
        var body = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        var responsesRequest = JsonNode.Parse(body)?.AsObject()
            ?? throw new InvalidOperationException("Request body was not valid JSON.");
        var chatRequest = BuildChatCompletionsRequest(responsesRequest);
        var upstream = settings.BaseUrl.TrimEnd('/') + "/chat/completions";

        using var request = new HttpRequestMessage(HttpMethod.Post, upstream)
        {
            Content = new StringContent(chatRequest.ToJsonString(JsonOptions), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            await WriteJsonAsync(context, response.StatusCode, new
            {
                error = string.IsNullOrWhiteSpace(error)
                    ? $"{(int)response.StatusCode} {response.ReasonPhrase}"
                    : error
            }, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (IsEventStream(response.Content.Headers.ContentType?.MediaType))
        {
            await StreamChatAsResponsesAsync(context, response, GetString(responsesRequest, "model") ?? "model", cancellationToken).ConfigureAwait(false);
        }
        else
        {
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            await WriteNonStreamingChatAsResponsesAsync(context, responseBody, GetString(responsesRequest, "model") ?? "model", cancellationToken).ConfigureAwait(false);
        }
    }

    private ProviderSettings? ResolveSettings(string providerId)
    {
        var saved = _settingsStore.Get(providerId);
        if (saved is not null)
        {
            return saved;
        }

        var preset = ProviderPreset.Find(providerId);
        if (preset is null || !preset.UseProxy)
        {
            return null;
        }

        return new ProviderSettings
        {
            ProviderId = preset.ProviderId,
            ProviderName = preset.ProviderName,
            BaseUrl = preset.BaseUrl,
            EnvKey = preset.EnvKey,
            Models = preset.Models.ToList()
        };
    }

    private static string? ResolveApiKey(ProviderSettings settings)
    {
        return WindowsCredentialStore.ReadSecret(settings.ProviderId)
            ?? Environment.GetEnvironmentVariable(settings.EnvKey, EnvironmentVariableTarget.Process)
            ?? Environment.GetEnvironmentVariable(settings.EnvKey, EnvironmentVariableTarget.User)
            ?? Environment.GetEnvironmentVariable(settings.EnvKey, EnvironmentVariableTarget.Machine);
    }

    private static JsonObject BuildChatCompletionsRequest(JsonObject responsesRequest)
    {
        var chatRequest = new JsonObject
        {
            ["model"] = responsesRequest["model"]?.DeepClone() ?? "model",
            ["messages"] = BuildMessages(responsesRequest),
            ["stream"] = true
        };

        if (responsesRequest["tools"] is JsonArray tools)
        {
            var chatTools = BuildTools(tools);
            if (chatTools.Count > 0)
            {
                chatRequest["tools"] = chatTools;
                chatRequest["tool_choice"] = MapToolChoice(responsesRequest["tool_choice"]);
            }
        }

        return chatRequest;
    }

    private static JsonArray BuildMessages(JsonObject responsesRequest)
    {
        var messages = new JsonArray();
        var instructions = GetString(responsesRequest, "instructions");
        if (!string.IsNullOrWhiteSpace(instructions))
        {
            messages.Add(new JsonObject
            {
                ["role"] = "system",
                ["content"] = instructions
            });
        }

        var input = responsesRequest["input"];
        if (input is JsonValue inputValue && inputValue.TryGetValue<string>(out var inputText))
        {
            AddTextMessage(messages, "user", inputText);
            return messages;
        }

        if (input is not JsonArray inputItems)
        {
            return messages;
        }

        foreach (var item in inputItems)
        {
            if (item is not JsonObject inputObject)
            {
                continue;
            }

            var type = GetString(inputObject, "type");
            if (type?.Equals("function_call_output", StringComparison.OrdinalIgnoreCase) == true)
            {
                AddToolMessage(messages, inputObject);
                continue;
            }

            if (type?.Equals("function_call", StringComparison.OrdinalIgnoreCase) == true)
            {
                AddAssistantToolCallMessage(messages, inputObject);
                continue;
            }

            var role = NormalizeRole(GetString(inputObject, "role"));
            var content = ExtractText(inputObject["content"]);
            AddTextMessage(messages, role, content);
        }

        return messages;
    }

    private static void AddTextMessage(JsonArray messages, string role, string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return;
        }

        messages.Add(new JsonObject
        {
            ["role"] = role,
            ["content"] = content
        });
    }

    private static void AddToolMessage(JsonArray messages, JsonObject inputObject)
    {
        var callId = GetString(inputObject, "call_id");
        var output = ExtractText(inputObject["output"]) ?? GetString(inputObject, "output") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(callId))
        {
            return;
        }

        messages.Add(new JsonObject
        {
            ["role"] = "tool",
            ["tool_call_id"] = callId,
            ["content"] = output
        });
    }

    private static void AddAssistantToolCallMessage(JsonArray messages, JsonObject inputObject)
    {
        var callId = GetString(inputObject, "call_id") ?? GetString(inputObject, "id");
        var name = GetString(inputObject, "name");
        var arguments = ExtractText(inputObject["arguments"]) ?? GetString(inputObject, "arguments") ?? "{}";
        if (string.IsNullOrWhiteSpace(callId) || string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        messages.Add(new JsonObject
        {
            ["role"] = "assistant",
            ["content"] = null,
            ["tool_calls"] = new JsonArray
            {
                new JsonObject
                {
                    ["id"] = callId,
                    ["type"] = "function",
                    ["function"] = new JsonObject
                    {
                        ["name"] = name,
                        ["arguments"] = arguments
                    }
                }
            }
        });
    }

    private static JsonArray BuildTools(JsonArray tools)
    {
        var chatTools = new JsonArray();
        foreach (var tool in tools)
        {
            if (tool is not JsonObject toolObject ||
                !string.Equals(GetString(toolObject, "type"), "function", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(GetString(toolObject, "name")))
            {
                continue;
            }

            chatTools.Add(new JsonObject
            {
                ["type"] = "function",
                ["function"] = new JsonObject
                {
                    ["name"] = GetString(toolObject, "name"),
                    ["description"] = GetString(toolObject, "description") ?? string.Empty,
                    ["parameters"] = toolObject["parameters"]?.DeepClone() ?? new JsonObject()
                }
            });
        }

        return chatTools;
    }

    private static JsonNode? MapToolChoice(JsonNode? toolChoice)
    {
        if (toolChoice is null)
        {
            return "auto";
        }

        if (toolChoice is JsonValue value && value.TryGetValue<string>(out var text))
        {
            return text is "none" or "required"
                ? text
                : "auto";
        }

        if (toolChoice is JsonObject obj &&
            string.Equals(GetString(obj, "type"), "function", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(GetString(obj, "name")))
        {
            return new JsonObject
            {
                ["type"] = "function",
                ["function"] = new JsonObject
                {
                    ["name"] = GetString(obj, "name")
                }
            };
        }

        return "auto";
    }

    private static string NormalizeRole(string? role)
    {
        return role?.ToLowerInvariant() switch
        {
            "assistant" => "assistant",
            "system" => "system",
            "developer" => "system",
            "tool" => "tool",
            _ => "user"
        };
    }

    private static string? ExtractText(JsonNode? node)
    {
        if (node is null)
        {
            return null;
        }

        if (node is JsonValue value && value.TryGetValue<string>(out var text))
        {
            return text;
        }

        if (node is JsonArray array)
        {
            var parts = new List<string>();
            foreach (var item in array)
            {
                var part = ExtractText(item);
                if (!string.IsNullOrWhiteSpace(part))
                {
                    parts.Add(part);
                }
            }

            return parts.Count == 0 ? null : string.Join(Environment.NewLine, parts);
        }

        if (node is JsonObject obj)
        {
            return GetString(obj, "text")
                ?? GetString(obj, "content")
                ?? GetString(obj, "output");
        }

        return null;
    }

    private async Task StreamChatAsResponsesAsync(HttpListenerContext context, HttpResponseMessage upstreamResponse, string model, CancellationToken cancellationToken)
    {
        context.Response.StatusCode = (int)HttpStatusCode.OK;
        context.Response.ContentType = "text/event-stream; charset=utf-8";
        context.Response.Headers["Cache-Control"] = "no-cache";

        await using var stream = await upstreamResponse.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var reader = new StreamReader(stream);
        await using var writer = new StreamWriter(context.Response.OutputStream, new UTF8Encoding(false), leaveOpen: true);
        var responses = new ResponsesSseWriter(writer, model);
        var toolCalls = new Dictionary<int, ToolCallBuffer>();

        await responses.StartAsync(cancellationToken).ConfigureAwait(false);
        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line is null || !line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var data = line[5..].Trim();
            if (data.Equals("[DONE]", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(data))
            {
                continue;
            }

            JsonObject? chunk;
            try
            {
                chunk = JsonNode.Parse(data)?.AsObject();
            }
            catch
            {
                continue;
            }

            var delta = chunk?["choices"]?[0]?["delta"]?.AsObject();
            var content = GetString(delta, "content");
            if (!string.IsNullOrEmpty(content))
            {
                await responses.WriteTextDeltaAsync(content, cancellationToken).ConfigureAwait(false);
            }

            if (delta?["tool_calls"] is JsonArray toolCallDeltas)
            {
                AccumulateToolCallDeltas(toolCalls, toolCallDeltas);
            }
        }

        foreach (var toolCall in toolCalls.OrderBy(pair => pair.Key).Select(pair => pair.Value))
        {
            await responses.WriteToolCallAsync(toolCall, cancellationToken).ConfigureAwait(false);
        }

        await responses.CompleteAsync(cancellationToken).ConfigureAwait(false);
        context.Response.Close();
    }

    private async Task WriteNonStreamingChatAsResponsesAsync(HttpListenerContext context, string responseBody, string model, CancellationToken cancellationToken)
    {
        context.Response.StatusCode = (int)HttpStatusCode.OK;
        context.Response.ContentType = "text/event-stream; charset=utf-8";
        context.Response.Headers["Cache-Control"] = "no-cache";

        await using var writer = new StreamWriter(context.Response.OutputStream, new UTF8Encoding(false), leaveOpen: true);
        var responses = new ResponsesSseWriter(writer, model);
        await responses.StartAsync(cancellationToken).ConfigureAwait(false);

        var json = JsonNode.Parse(responseBody)?.AsObject();
        var message = json?["choices"]?[0]?["message"]?.AsObject();
        var content = GetString(message, "content");
        if (!string.IsNullOrEmpty(content))
        {
            await responses.WriteTextDeltaAsync(content, cancellationToken).ConfigureAwait(false);
        }

        if (message?["tool_calls"] is JsonArray toolCalls)
        {
            foreach (var toolCall in toolCalls)
            {
                if (toolCall is JsonObject toolObject)
                {
                    await responses.WriteToolCallAsync(ToolCallBuffer.FromChatToolCall(toolObject), cancellationToken).ConfigureAwait(false);
                }
            }
        }

        await responses.CompleteAsync(cancellationToken).ConfigureAwait(false);
        context.Response.Close();
    }

    private static void AccumulateToolCallDeltas(Dictionary<int, ToolCallBuffer> toolCalls, JsonArray deltas)
    {
        foreach (var deltaNode in deltas)
        {
            if (deltaNode is not JsonObject delta)
            {
                continue;
            }

            var index = GetInt(delta, "index") ?? toolCalls.Count;
            if (!toolCalls.TryGetValue(index, out var buffer))
            {
                buffer = new ToolCallBuffer();
                toolCalls[index] = buffer;
            }

            buffer.CallId ??= GetString(delta, "id");
            if (delta["function"] is JsonObject function)
            {
                buffer.Name ??= GetString(function, "name");
                buffer.Arguments.Append(GetString(function, "arguments"));
            }
        }
    }

    private static bool IsEventStream(string? mediaType)
    {
        return mediaType?.Contains("text/event-stream", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static string? GetString(JsonObject? obj, string propertyName)
    {
        if (obj is null ||
            !obj.TryGetPropertyValue(propertyName, out var node) ||
            node is not JsonValue value)
        {
            return null;
        }

        return value.TryGetValue<string>(out var result) ? result : null;
    }

    private static int? GetInt(JsonObject obj, string propertyName)
    {
        if (!obj.TryGetPropertyValue(propertyName, out var node) ||
            node is not JsonValue value)
        {
            return null;
        }

        return value.TryGetValue<int>(out var result) ? result : null;
    }

    private static async Task WriteJsonAsync(HttpListenerContext context, HttpStatusCode statusCode, object payload, CancellationToken cancellationToken)
    {
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, JsonOptions));
        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.ContentLength64 = bytes.Length;
        await context.Response.OutputStream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        context.Response.Close();
    }
}

internal sealed class ToolCallBuffer
{
    public string? CallId { get; set; }
    public string? Name { get; set; }
    public StringBuilder Arguments { get; } = new();

    public static ToolCallBuffer FromChatToolCall(JsonObject toolCall)
    {
        var buffer = new ToolCallBuffer
        {
            CallId = GetString(toolCall, "id")
        };

        if (toolCall["function"] is JsonObject function)
        {
            buffer.Name = GetString(function, "name");
            buffer.Arguments.Append(GetString(function, "arguments"));
        }

        return buffer;
    }

    private static string? GetString(JsonObject obj, string propertyName)
    {
        if (!obj.TryGetPropertyValue(propertyName, out var node) ||
            node is not JsonValue value)
        {
            return null;
        }

        return value.TryGetValue<string>(out var result) ? result : null;
    }
}

internal sealed class ResponsesSseWriter
{
    private readonly StreamWriter _writer;
    private readonly string _model;
    private readonly string _responseId = "resp_" + Guid.NewGuid().ToString("N");
    private readonly long _createdAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    private readonly List<object> _output = new();
    private string? _messageItemId;
    private readonly StringBuilder _messageText = new();
    private int _outputIndex;

    public ResponsesSseWriter(StreamWriter writer, string model)
    {
        _writer = writer;
        _model = model;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return WriteEventAsync("response.created", new
        {
            type = "response.created",
            response = BuildResponse("in_progress")
        }, cancellationToken);
    }

    public async Task WriteTextDeltaAsync(string delta, CancellationToken cancellationToken)
    {
        if (_messageItemId is null)
        {
            _messageItemId = "msg_" + Guid.NewGuid().ToString("N");
            await WriteEventAsync("response.output_item.added", new
            {
                type = "response.output_item.added",
                output_index = _outputIndex,
                item = new
                {
                    id = _messageItemId,
                    type = "message",
                    status = "in_progress",
                    role = "assistant",
                    content = Array.Empty<object>()
                }
            }, cancellationToken).ConfigureAwait(false);
            await WriteEventAsync("response.content_part.added", new
            {
                type = "response.content_part.added",
                item_id = _messageItemId,
                output_index = _outputIndex,
                content_index = 0,
                part = new
                {
                    type = "output_text",
                    text = string.Empty
                }
            }, cancellationToken).ConfigureAwait(false);
        }

        _messageText.Append(delta);
        await WriteEventAsync("response.output_text.delta", new
        {
            type = "response.output_text.delta",
            item_id = _messageItemId,
            output_index = _outputIndex,
            content_index = 0,
            delta
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task WriteToolCallAsync(ToolCallBuffer toolCall, CancellationToken cancellationToken)
    {
        await FinishTextMessageAsync(cancellationToken).ConfigureAwait(false);

        var itemId = "fc_" + Guid.NewGuid().ToString("N");
        var callId = string.IsNullOrWhiteSpace(toolCall.CallId)
            ? "call_" + Guid.NewGuid().ToString("N")
            : toolCall.CallId;
        var name = string.IsNullOrWhiteSpace(toolCall.Name) ? "tool" : toolCall.Name;
        var arguments = toolCall.Arguments.ToString();
        var outputIndex = _outputIndex++;

        await WriteEventAsync("response.output_item.added", new
        {
            type = "response.output_item.added",
            output_index = outputIndex,
            item = new
            {
                id = itemId,
                type = "function_call",
                status = "in_progress",
                call_id = callId,
                name,
                arguments = string.Empty
            }
        }, cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrEmpty(arguments))
        {
            await WriteEventAsync("response.function_call_arguments.delta", new
            {
                type = "response.function_call_arguments.delta",
                item_id = itemId,
                output_index = outputIndex,
                delta = arguments
            }, cancellationToken).ConfigureAwait(false);
        }

        await WriteEventAsync("response.function_call_arguments.done", new
        {
            type = "response.function_call_arguments.done",
            item_id = itemId,
            output_index = outputIndex,
            arguments
        }, cancellationToken).ConfigureAwait(false);

        var item = new
        {
            id = itemId,
            type = "function_call",
            status = "completed",
            call_id = callId,
            name,
            arguments
        };
        _output.Add(item);

        await WriteEventAsync("response.output_item.done", new
        {
            type = "response.output_item.done",
            output_index = outputIndex,
            item
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task CompleteAsync(CancellationToken cancellationToken)
    {
        await FinishTextMessageAsync(cancellationToken).ConfigureAwait(false);
        await WriteEventAsync("response.completed", new
        {
            type = "response.completed",
            response = BuildResponse("completed")
        }, cancellationToken).ConfigureAwait(false);
        await _writer.WriteLineAsync("data: [DONE]").ConfigureAwait(false);
        await _writer.WriteLineAsync().ConfigureAwait(false);
        await _writer.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task FinishTextMessageAsync(CancellationToken cancellationToken)
    {
        if (_messageItemId is null)
        {
            return;
        }

        var text = _messageText.ToString();
        var outputIndex = _outputIndex++;

        await WriteEventAsync("response.output_text.done", new
        {
            type = "response.output_text.done",
            item_id = _messageItemId,
            output_index = outputIndex,
            content_index = 0,
            text
        }, cancellationToken).ConfigureAwait(false);

        await WriteEventAsync("response.content_part.done", new
        {
            type = "response.content_part.done",
            item_id = _messageItemId,
            output_index = outputIndex,
            content_index = 0,
            part = new
            {
                type = "output_text",
                text
            }
        }, cancellationToken).ConfigureAwait(false);

        var item = new
        {
            id = _messageItemId,
            type = "message",
            status = "completed",
            role = "assistant",
            content = new[]
            {
                new
                {
                    type = "output_text",
                    text
                }
            }
        };
        _output.Add(item);

        await WriteEventAsync("response.output_item.done", new
        {
            type = "response.output_item.done",
            output_index = outputIndex,
            item
        }, cancellationToken).ConfigureAwait(false);

        _messageItemId = null;
        _messageText.Clear();
    }

    private object BuildResponse(string status)
    {
        return new
        {
            id = _responseId,
            @object = "response",
            created_at = _createdAt,
            status,
            model = _model,
            output = _output.ToArray()
        };
    }

    private async Task WriteEventAsync(string eventName, object payload, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(payload, ProviderProxyServerJson.Options);
        await _writer.WriteAsync("event: ").ConfigureAwait(false);
        await _writer.WriteLineAsync(eventName).ConfigureAwait(false);
        await _writer.WriteAsync("data: ").ConfigureAwait(false);
        await _writer.WriteLineAsync(json).ConfigureAwait(false);
        await _writer.WriteLineAsync().ConfigureAwait(false);
        await _writer.FlushAsync(cancellationToken).ConfigureAwait(false);
    }
}

internal static class ProviderProxyServerJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };
}
