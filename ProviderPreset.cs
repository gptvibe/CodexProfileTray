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
            DisplayName = "OpenAI-compatible API",
            ProviderId = "custom",
            ProviderName = "Custom Provider",
            BaseUrl = "https://api.example.com/v1",
            EnvKey = "CODEX_PROFILE_TRAY_CUSTOM_API_KEY",
            IsCustom = true
        }
    };
}
