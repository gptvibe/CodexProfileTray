using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace CodexProfileTray;

internal sealed class CodexConfigManager
{
    private static readonly Regex SectionRegex = new(@"^\s*\[(?<section>[^\]]+)\]\s*$", RegexOptions.Compiled);
    private static readonly Regex KeyValueRegex = new(@"^\s*(?<key>[A-Za-z0-9_\-]+)\s*=\s*(?<value>.+?)\s*$", RegexOptions.Compiled);

    public string CodexHome { get; }
    public string ConfigPath { get; }

    public CodexConfigManager()
    {
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

            result.Add(new CodexProfile
            {
                ProfileName = pair.Key,
                ProviderId = providerId,
                ProviderName = Get(provider, "name") ?? providerId,
                BaseUrl = Get(provider, "base_url"),
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

    public void UpsertProfile(ProfileDefinition definition)
    {
        Directory.CreateDirectory(CodexHome);
        var lines = File.Exists(ConfigPath)
            ? File.ReadAllLines(ConfigPath).ToList()
            : new List<string>();

        BackupConfigIfExists();
        RemoveSection(lines, $"model_providers.{definition.ProviderId}");
        RemoveSection(lines, $"profiles.{definition.ProfileName}");

        if (lines.Count > 0 && !string.IsNullOrWhiteSpace(lines[^1]))
        {
            lines.Add(string.Empty);
        }

        lines.Add("# Added by Codex Profile Tray.");
        lines.Add($"[model_providers.{QuoteTomlKey(definition.ProviderId)}]");
        lines.Add($"name = {QuoteTomlString(definition.ProviderName)}");
        lines.Add($"base_url = {QuoteTomlString(definition.BaseUrl)}");
        lines.Add($"env_key = {QuoteTomlString(definition.EnvKey)}");
        lines.Add($"env_key_instructions = {QuoteTomlString($"Set {definition.EnvKey} to your API key.")}");
        lines.Add("request_max_retries = 3");
        lines.Add("stream_max_retries = 2");
        lines.Add("stream_idle_timeout_ms = 600000");
        lines.Add(string.Empty);
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

    private void BackupConfigIfExists()
    {
        if (!File.Exists(ConfigPath))
        {
            return;
        }

        var backupPath = ConfigPath + ".bak-codex-profile-tray-" + DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
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
