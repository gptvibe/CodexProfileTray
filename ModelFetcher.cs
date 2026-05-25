using System.Net.Http.Headers;
using System.Text.Json;

namespace CodexProfileTray;

internal static class ModelFetcher
{
    public static async Task<IReadOnlyList<string>> FetchAsync(string baseUrl, string? apiKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException("Enter a base URL first.");
        }

        var endpoint = baseUrl.TrimEnd('/') + "/models";
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }

        using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Model fetch failed: {(int)response.StatusCode} {response.ReasonPhrase}");
        }

        return ParseModelIds(body);
    }

    private static IReadOnlyList<string> ParseModelIds(string json)
    {
        using var document = JsonDocument.Parse(json);
        var ids = new List<string>();

        if (document.RootElement.ValueKind == JsonValueKind.Object &&
            document.RootElement.TryGetProperty("data", out var data) &&
            data.ValueKind == JsonValueKind.Array)
        {
            AddIds(data, ids);
        }
        else if (document.RootElement.ValueKind == JsonValueKind.Array)
        {
            AddIds(document.RootElement, ids);
        }

        return ids
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void AddIds(JsonElement array, List<string> ids)
    {
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                ids.Add(item.GetString() ?? string.Empty);
                continue;
            }

            if (item.ValueKind == JsonValueKind.Object &&
                item.TryGetProperty("id", out var id) &&
                id.ValueKind == JsonValueKind.String)
            {
                ids.Add(id.GetString() ?? string.Empty);
            }
        }
    }
}
