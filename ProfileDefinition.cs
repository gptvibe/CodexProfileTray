namespace CodexProfileTray;

internal sealed class ProfileDefinition
{
    public required string ProfileName { get; init; }
    public required string ProviderId { get; init; }
    public required string ProviderName { get; init; }
    public required string BaseUrl { get; init; }
    public required string EnvKey { get; init; }
    public required string Model { get; init; }
    public string? ReasoningEffort { get; init; }
    public int? ContextWindow { get; init; }
    public bool? SupportsReasoningSummaries { get; init; }
}
