using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace CodexProfileTray;

internal sealed class CodexConfigManager
{
    private static readonly Regex SectionRegex = new(@"^\s*\[(?<section>[^\]]+)\]\s*$", RegexOptions.Compiled);
    private static readonly Regex KeyValueRegex = new(@"^\s*(?<key>[A-Za-z0-9_\-]+)\s*=\s*(?<value>.+?)\s*$", RegexOptions.Compiled);
    private readonly ProviderSettingsStore _providerSettingsStore;

    public string CodexHome { get; }
    public string ConfigPath { get; }

    public CodexConfigManager(ProviderSettingsStore? providerSettingsStore = null)
    {
        _providerSettingsStore = providerSettingsStore ?? new ProviderSettingsStore();
        var codexHome = Environment.GetEnvironmentVariable("CODEX_HOME");
        if (string.IsNullOrWhiteSpace(codexHome))
        {
            codexHome = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex");
        }

        CodexHome = codexHome;
        ConfigPath = Path.Combine(CodexHome, "config.toml");
    }

    public IReadOnlyList<CodexProfile> LoadProfiles()
    {
        var sections = LoadSections();
        var providers = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        var profiles = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var section in sections)
        {
            if (TryStripPrefix(section.Name, "model_providers.", out var providerId))
            {
                providers[providerId] = section.Values;
            }
            else if (TryStripPrefix(section.Name, "profiles.", out var profileName))
            {
                profiles[profileName] = section.Values;
            }
        }

        var result = new List<CodexProfile>();
        foreach (var pair in profiles.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
        {
            var values = pair.Value;
            var providerId = Get(values, "model_provider") ?? "openai";
            providers.TryGetValue(providerId, out var provider);
            var providerBaseUrl = Get(provider, "base_url");
            if (ProviderProxyServer.TryGetProviderId(providerBaseUrl, out _) &&
                _providerSettingsStore.Get(providerId) is { } settings)
            {
                providerBaseUrl = settings.BaseUrl;
            }

            result.Add(new CodexProfile
            {
                ProfileName = pair.Key,
                ProviderId = providerId,
                ProviderName = Get(provider, "name") ?? providerId,
                BaseUrl = providerBaseUrl,
                EnvKey = Get(provider, "env_key"),
                Model = Get(values, "model"),
                ReasoningEffort = Get(values, "model_reasoning_effort"),
                ContextWindow = TryParseInt(Get(values, "model_context_window")),
                SupportsReasoningSummaries = TryParseBool(Get(values, "model_supports_reasoning_summaries")),
                IsBuiltInOpenAI = providerId.Equals("openai", StringComparison.OrdinalIgnoreCase)
            });
        }

        return result;
    }

    public void EnsureProviderCompatibility(CodexProfile profile)
    {
        if (!ProviderProxyServer.ShouldProxyProvider(profile.ProviderId) ||
            string.IsNullOrWhiteSpace(profile.Model))
        {
            return;
        }

        var rawBaseUrl = GetProviderValue(profile.ProviderId, "base_url");
        var savedSettings = _providerSettingsStore.Get(profile.ProviderId);
        if (ProviderProxyServer.TryGetProviderId(rawBaseUrl, out _) && savedSettings is not null)
        {
            return;
        }

        var preset = ProviderPreset.Find(profile.ProviderId);
        var upstreamBaseUrl = savedSettings?.BaseUrl;
        if (string.IsNullOrWhiteSpace(upstreamBaseUrl) &&
            !ProviderProxyServer.TryGetProviderId(profile.BaseUrl, out _))
        {
            upstreamBaseUrl = profile.BaseUrl;
        }

        upstreamBaseUrl ??= preset?.BaseUrl;
        if (string.IsNullOrWhiteSpace(upstreamBaseUrl))
        {
            throw new InvalidOperationException($"Open Manage Providers and save '{profile.ProviderName ?? profile.ProviderId}' again so the tray can route it through the compatibility proxy.");
        }

        var models = new List<string>();
        if (preset is not null)
        {
            models.AddRange(preset.Models);
        }

        if (savedSettings is not null)
        {
            models.AddRange(savedSettings.Models);
        }

        models.Add(profile.Model);

        UpsertProfile(new ProfileDefinition
        {
            ProfileName = profile.ProfileName,
            ProviderId = profile.ProviderId,
            ProviderName = profile.ProviderName ?? preset?.ProviderName ?? profile.ProviderId,
            BaseUrl = upstreamBaseUrl,
            EnvKey = profile.EnvKey ?? savedSettings?.EnvKey ?? preset?.EnvKey ?? MakeEnvKey(profile.ProviderId),
            Model = profile.Model,
            UseProxy = true,
            KnownModels = models,
            ReasoningEffort = profile.ReasoningEffort,
            ContextWindow = profile.ContextWindow,
            SupportsReasoningSummaries = profile.SupportsReasoningSummaries
        });
    }

    public void SetActiveProfile(CodexProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.Model))
        {
            throw new InvalidOperationException($"Profile '{profile.ProfileName}' does not have a model.");
        }

        Directory.CreateDirectory(CodexHome);
        var originalLines = File.Exists(ConfigPath)
            ? File.ReadAllLines(ConfigPath).ToList()
            : new List<string>();
        var lines = originalLines.ToList();

        RemoveTopLevelKeys(lines, new[]
        {
            "model_provider",
            "model",
            "model_reasoning_effort",
            "model_context_window",
            "model_supports_reasoning_summaries"
        });

        var activeLines = new List<string>
        {
            "# Active profile selected by Codex Profile Tray.",
            $"model_provider = {QuoteTomlString(profile.ProviderId)}",
            $"model = {QuoteTomlString(profile.Model)}"
        };

        if (!string.IsNullOrWhiteSpace(profile.ReasoningEffort))
        {
            activeLines.Add($"model_reasoning_effort = {QuoteTomlString(profile.ReasoningEffort)}");
        }

        if (profile.ContextWindow.HasValue)
        {
            activeLines.Add($"model_context_window = {profile.ContextWindow.Value.ToString(CultureInfo.InvariantCulture)}");
        }

        if (profile.SupportsReasoningSummaries.HasValue)
        {
            activeLines.Add($"model_supports_reasoning_summaries = {profile.SupportsReasoningSummaries.Value.ToString().ToLowerInvariant()}");
        }

        activeLines.Add(string.Empty);
        lines.InsertRange(0, activeLines);

        if (string.Join('\n', originalLines) == string.Join('\n', lines))
        {
            return;
        }

        BackupConfigIfExists();
        File.WriteAllLines(ConfigPath, lines, new UTF8Encoding(false));
    }

    public void UpsertProfile(ProfileDefinition definition)
    {
        UpsertProfiles(new[] { definition });
    }

    public void UpsertProfiles(IReadOnlyList<ProfileDefinition> definitions)
    {
        Directory.CreateDirectory(CodexHome);
        if (definitions.Count == 0)
        {
            return;
        }

        var lines = File.Exists(ConfigPath)
            ? File.ReadAllLines(ConfigPath).ToList()
            : new List<string>();

        BackupConfigIfExists();
        foreach (var providerId in definitions.Select(definition => definition.ProviderId).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            RemoveSection(lines, $"model_providers.{providerId}");
        }

        foreach (var definition in definitions)
        {
            RemoveSection(lines, $"profiles.{definition.ProfileName}");
        }

        if (lines.Count > 0 && !string.IsNullOrWhiteSpace(lines[^1]))
        {
            lines.Add(string.Empty);
        }

        foreach (var provider in definitions
                     .GroupBy(definition => definition.ProviderId, StringComparer.OrdinalIgnoreCase)
                     .Select(group => group.First()))
        {
            if (provider.UseProxy)
            {
                _providerSettingsStore.Upsert(provider);
            }

            var providerBaseUrl = provider.UseProxy
                ? ProviderProxyServer.GetProviderBaseUrl(provider.ProviderId)
                : provider.BaseUrl;

            lines.Add("# Added by Codex Profile Tray.");
            lines.Add($"[model_providers.{QuoteTomlKey(provider.ProviderId)}]");
            lines.Add($"name = {QuoteTomlString(provider.ProviderName)}");
            lines.Add($"base_url = {QuoteTomlString(providerBaseUrl)}");
            lines.Add("wire_api = \"responses\"");
            lines.Add($"env_key = {QuoteTomlString(provider.EnvKey)}");
            lines.Add($"env_key_instructions = {QuoteTomlString($"Set {provider.EnvKey} to your API key.")}");
            lines.Add("request_max_retries = 3");
            lines.Add("stream_max_retries = 2");
            lines.Add("stream_idle_timeout_ms = 600000");
            lines.Add(string.Empty);
        }

        foreach (var definition in definitions)
        {
            lines.Add($"[profiles.{QuoteTomlKey(definition.ProfileName)}]");
            lines.Add($"model_provider = {QuoteTomlString(definition.ProviderId)}");
            lines.Add($"model = {QuoteTomlString(definition.Model)}");

            if (!string.IsNullOrWhiteSpace(definition.ReasoningEffort))
            {
                lines.Add($"model_reasoning_effort = {QuoteTomlString(definition.ReasoningEffort)}");
            }

            if (definition.ContextWindow.HasValue)
            {
                lines.Add($"model_context_window = {definition.ContextWindow.Value.ToString(CultureInfo.InvariantCulture)}");
            }

            if (definition.SupportsReasoningSummaries.HasValue)
            {
                lines.Add($"model_supports_reasoning_summaries = {definition.SupportsReasoningSummaries.Value.ToString().ToLowerInvariant()}");
            }

            lines.Add(string.Empty);
        }

        File.WriteAllLines(ConfigPath, lines, new UTF8Encoding(false));
    }

    public void DeleteProfile(CodexProfile profile)
    {
        if (profile.IsBuiltInOpenAI)
        {
            throw new InvalidOperationException("The built-in OpenAI profile is managed by Codex and should not be deleted here.");
        }

        var otherProfilesUsingProvider = LoadProfiles()
            .Any(existing =>
                !existing.ProfileName.Equals(profile.ProfileName, StringComparison.OrdinalIgnoreCase) &&
                existing.ProviderId.Equals(profile.ProviderId, StringComparison.OrdinalIgnoreCase));

        var lines = File.Exists(ConfigPath)
            ? File.ReadAllLines(ConfigPath).ToList()
            : new List<string>();

        BackupConfigIfExists();
        RemoveSection(lines, $"profiles.{profile.ProfileName}");
        if (!otherProfilesUsingProvider)
        {
            RemoveSection(lines, $"model_providers.{profile.ProviderId}");
            _providerSettingsStore.Delete(profile.ProviderId);
        }

        File.WriteAllLines(ConfigPath, lines, new UTF8Encoding(false));
    }

    public static string MakeProviderId(string profileName)
    {
        var cleaned = Regex.Replace(profileName.Trim().ToLowerInvariant(), @"[^a-z0-9_\-]+", "-").Trim('-');
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            cleaned = "custom";
        }

        if (!char.IsLetter(cleaned[0]))
        {
            cleaned = "provider-" + cleaned;
        }

        return cleaned;
    }

    public static string MakeProfileName(string providerId, string model)
    {
        var modelSlug = Regex.Replace(model.Trim().ToLowerInvariant(), @"[^a-z0-9_\-]+", "-").Trim('-');
        if (string.IsNullOrWhiteSpace(modelSlug))
        {
            modelSlug = "model";
        }

        if (modelSlug.StartsWith(providerId, StringComparison.OrdinalIgnoreCase))
        {
            return modelSlug;
        }

        return $"{providerId}-{modelSlug}";
    }

    public static string MakeEnvKey(string providerId)
    {
        var cleaned = Regex.Replace(providerId.ToUpperInvariant(), @"[^A-Z0-9]+", "_").Trim('_');
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            cleaned = "CUSTOM";
        }

        return $"CODEX_PROFILE_TRAY_{cleaned}_API_KEY";
    }

    private List<ConfigSection> LoadSections()
    {
        var sections = new List<ConfigSection>();
        if (!File.Exists(ConfigPath))
        {
            return sections;
        }

        ConfigSection? current = null;
        foreach (var line in File.ReadAllLines(ConfigPath))
        {
            var sectionMatch = SectionRegex.Match(line);
            if (sectionMatch.Success)
            {
                current = new ConfigSection(NormalizeSectionName(sectionMatch.Groups["section"].Value));
                sections.Add(current);
                continue;
            }

            if (current is null)
            {
                continue;
            }

            var kv = KeyValueRegex.Match(line);
            if (kv.Success)
            {
                current.Values[kv.Groups["key"].Value] = ParseTomlValue(kv.Groups["value"].Value);
            }
        }

        return sections;
    }

    private string? GetProviderValue(string providerId, string key)
    {
        foreach (var section in LoadSections())
        {
            if (TryStripPrefix(section.Name, "model_providers.", out var sectionProviderId) &&
                sectionProviderId.Equals(providerId, StringComparison.OrdinalIgnoreCase))
            {
                return Get(section.Values, key);
            }
        }

        return null;
    }

    private void BackupConfigIfExists()
    {
        if (!File.Exists(ConfigPath))
        {
            return;
        }

        var backupPath = ConfigPath + ".bak-codex-profile-tray-" + DateTime.Now.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture);
        File.Copy(ConfigPath, backupPath, overwrite: false);
    }

    private static void RemoveSection(List<string> lines, string sectionName)
    {
        var normalized = NormalizeSectionName(sectionName);
        for (var index = 0; index < lines.Count;)
        {
            var match = SectionRegex.Match(lines[index]);
            if (!match.Success || !NormalizeSectionName(match.Groups["section"].Value).Equals(normalized, StringComparison.OrdinalIgnoreCase))
            {
                index++;
                continue;
            }

            var start = index;
            index++;
            while (index < lines.Count && !SectionRegex.IsMatch(lines[index]))
            {
                index++;
            }

            while (start > 0 && string.IsNullOrWhiteSpace(lines[start - 1]))
            {
                start--;
            }

            lines.RemoveRange(start, index - start);
            index = start;
        }
    }

    private static void RemoveTopLevelKeys(List<string> lines, IReadOnlyCollection<string> keys)
    {
        for (var index = 0; index < lines.Count;)
        {
            if (SectionRegex.IsMatch(lines[index]))
            {
                break;
            }

            var keyMatch = KeyValueRegex.Match(lines[index]);
            if ((keyMatch.Success && keys.Contains(keyMatch.Groups["key"].Value)) ||
                lines[index].Trim().Equals("# Active profile selected by Codex Profile Tray.", StringComparison.Ordinal))
            {
                lines.RemoveAt(index);
                continue;
            }

            index++;
        }

        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[0]))
        {
            lines.RemoveAt(0);
        }
    }

    private static bool TryStripPrefix(string sectionName, string prefix, out string remainder)
    {
        if (sectionName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            remainder = sectionName[prefix.Length..];
            return true;
        }

        remainder = string.Empty;
        return false;
    }

    private static string? Get(Dictionary<string, string>? values, string key)
    {
        if (values is null)
        {
            return null;
        }

        return values.TryGetValue(key, out var value) ? value : null;
    }

    private static int? TryParseInt(string? value)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : null;
    }

    private static bool? TryParseBool(string? value)
    {
        return bool.TryParse(value, out var result) ? result : null;
    }

    private static string ParseTomlValue(string value)
    {
        value = value.Trim();
        var commentIndex = value.IndexOf(" #", StringComparison.Ordinal);
        if (commentIndex >= 0)
        {
            value = value[..commentIndex].TrimEnd();
        }

        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
        {
            return Regex.Unescape(value[1..^1]);
        }

        if (value.Length >= 2 && value[0] == '\'' && value[^1] == '\'')
        {
            return value[1..^1];
        }

        return value.Trim();
    }

    private static string NormalizeSectionName(string section)
    {
        var parts = SplitTomlDottedName(section.Trim());
        return string.Join(".", parts);
    }

    private static IReadOnlyList<string> SplitTomlDottedName(string value)
    {
        var parts = new List<string>();
        var current = new StringBuilder();
        var inQuote = false;
        var quoteChar = '\0';

        for (var index = 0; index < value.Length; index++)
        {
            var ch = value[index];
            if (inQuote)
            {
                if (ch == '\\' && quoteChar == '"' && index + 1 < value.Length)
                {
                    index++;
                    current.Append(value[index]);
                }
                else if (ch == quoteChar)
                {
                    inQuote = false;
                }
                else
                {
                    current.Append(ch);
                }
                continue;
            }

            if (ch == '"' || ch == '\'')
            {
                inQuote = true;
                quoteChar = ch;
                continue;
            }

            if (ch == '.')
            {
                parts.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(ch);
        }

        parts.Add(current.ToString());
        return parts;
    }

    private static string QuoteTomlKey(string key)
    {
        if (Regex.IsMatch(key, @"^[A-Za-z0-9_\-]+$"))
        {
            return key;
        }

        return QuoteTomlString(key);
    }

    private static string QuoteTomlString(string value)
    {
        return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }

    private sealed class ConfigSection(string name)
    {
        public string Name { get; } = name;
        public Dictionary<string, string> Values { get; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
