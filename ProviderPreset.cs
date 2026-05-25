namespace CodexProfileTray;

internal sealed class ProviderPreset
{
    public required string DisplayName { get; init; }
    public required string ProviderId { get; init; }
    public required string ProviderName { get; init; }
    public required string BaseUrl { get; init; }
    public required string EnvKey { get; init; }
    public IReadOnlyList<string> Models { get; init; } = Array.Empty<string>();
    public int? ContextWindow { get; init; }
    public string? ReasoningEffort { get; init; }
    public bool? SupportsReasoningSummaries { get; init; }
    public bool IsCustom { get; init; }

    public override string ToString() => DisplayName;

    public static IReadOnlyList<ProviderPreset> All { get; } = new[]
    {
        new ProviderPreset
        {
            DisplayName = "DeepSeek",
            ProviderId = "deepseek",
            ProviderName = "DeepSeek",
            BaseUrl = "https://api.deepseek.com",
            EnvKey = "DEEPSEEK_API_KEY",
            Models = new[] { "deepseek-v4-pro", "deepseek-v4-flash" },
            ContextWindow = 1_000_000,
            ReasoningEffort = "high",
            SupportsReasoningSummaries = false
        },
        new ProviderPreset
        {
            DisplayName = "Custom OpenAI-compatible API",
            ProviderId = "custom",
            ProviderName = "Custom Provider",
            BaseUrl = "https://api.example.com/v1",
            EnvKey = "CODEX_PROFILE_TRAY_CUSTOM_API_KEY",
            IsCustom = true
        }
    };
}
