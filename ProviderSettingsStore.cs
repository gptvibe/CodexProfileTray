using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace CodexProfileTray;

internal sealed class ProviderSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver()
    };

    private readonly object _gate = new();

    public ProviderSettingsStore()
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "CodexProfileTray");
        SettingsPath = Path.Combine(root, "providers.json");
    }

    public string SettingsPath { get; }

    public IReadOnlyList<ProviderSettings> LoadAll()
    {
        lock (_gate)
        {
            return LoadAllUnsafe()
                .Values
                .OrderBy(item => item.ProviderId, StringComparer.OrdinalIgnoreCase)
                .Select(Copy)
                .ToList();
        }
    }

    public ProviderSettings? Get(string providerId)
    {
        if (string.IsNullOrWhiteSpace(providerId))
        {
            return null;
        }

        lock (_gate)
        {
            var providers = LoadAllUnsafe();
            return providers.TryGetValue(providerId, out var settings)
                ? Copy(settings)
                : null;
        }
    }

    public void Upsert(ProfileDefinition definition)
    {
        if (!definition.UseProxy)
        {
            return;
        }

        var knownModels = definition.KnownModels
            .Append(definition.Model)
            .Where(model => !string.IsNullOrWhiteSpace(model))
            .Select(model => model.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(model => model, StringComparer.OrdinalIgnoreCase)
            .ToList();

        Upsert(new ProviderSettings
        {
            ProviderId = definition.ProviderId,
            ProviderName = definition.ProviderName,
            BaseUrl = definition.BaseUrl,
            EnvKey = definition.EnvKey,
            Models = knownModels
        });
    }

    public void Upsert(ProviderSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.ProviderId) ||
            string.IsNullOrWhiteSpace(settings.BaseUrl))
        {
            return;
        }

        lock (_gate)
        {
            var providers = LoadAllUnsafe();
            if (providers.TryGetValue(settings.ProviderId, out var existing))
            {
                settings.Models = existing.Models
                    .Concat(settings.Models)
                    .Where(model => !string.IsNullOrWhiteSpace(model))
                    .Select(model => model.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(model => model, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            providers[settings.ProviderId] = Copy(settings);
            SaveAllUnsafe(providers);
        }
    }

    public void Delete(string providerId)
    {
        if (string.IsNullOrWhiteSpace(providerId))
        {
            return;
        }

        lock (_gate)
        {
            var providers = LoadAllUnsafe();
            if (!providers.Remove(providerId))
            {
                return;
            }

            SaveAllUnsafe(providers);
        }
    }

    private Dictionary<string, ProviderSettings> LoadAllUnsafe()
    {
        if (!File.Exists(SettingsPath))
        {
            return new Dictionary<string, ProviderSettings>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            var items = JsonSerializer.Deserialize<List<ProviderSettings>>(File.ReadAllText(SettingsPath), JsonOptions)
                ?? new List<ProviderSettings>();
            return items
                .Where(item => !string.IsNullOrWhiteSpace(item.ProviderId))
                .ToDictionary(item => item.ProviderId, Copy, StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, ProviderSettings>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private void SaveAllUnsafe(Dictionary<string, ProviderSettings> providers)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        var ordered = providers.Values
            .OrderBy(item => item.ProviderId, StringComparer.OrdinalIgnoreCase)
            .Select(Copy)
            .ToList();
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(ordered, JsonOptions));
    }

    private static ProviderSettings Copy(ProviderSettings settings)
    {
        return new ProviderSettings
        {
            ProviderId = settings.ProviderId,
            ProviderName = settings.ProviderName,
            BaseUrl = settings.BaseUrl,
            EnvKey = settings.EnvKey,
            Models = settings.Models
                .Where(model => !string.IsNullOrWhiteSpace(model))
                .Select(model => model.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(model => model, StringComparer.OrdinalIgnoreCase)
                .ToList()
        };
    }
}

internal sealed class ProviderSettings
{
    public required string ProviderId { get; init; }
    public required string ProviderName { get; init; }
    public required string BaseUrl { get; init; }
    public required string EnvKey { get; init; }
    public List<string> Models { get; set; } = new();
}
