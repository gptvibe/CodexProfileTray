namespace CodexProfileTray;

internal sealed class CodexProfile
{
    public required string ProfileName { get; init; }
    public required string ProviderId { get; init; }
    public string? ProviderName { get; init; }
    public string? BaseUrl { get; init; }
    public string? EnvKey { get; init; }
    public string? Model { get; init; }
    public string? ReasoningEffort { get; init; }
    public int? ContextWindow { get; init; }
    public bool? SupportsReasoningSummaries { get; init; }
    public bool IsBuiltInOpenAI { get; init; }

    public override string ToString()
    {
        if (string.IsNullOrWhiteSpace(Model))
        {
            return ProfileName;
        }

        return $"{ProfileName}  ({Model})";
    }
}
