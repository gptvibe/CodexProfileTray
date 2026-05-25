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
            DisplayName = "OpenAI API",
            ProviderId = "openai-api",
            ProviderName = "OpenAI API",
            BaseUrl = "https://api.openai.com/v1",
            EnvKey = "OPENAI_API_KEY"
        },
        new ProviderPreset
        {
            DisplayName = "OpenRouter",
            ProviderId = "openrouter",
            ProviderName = "OpenRouter",
            BaseUrl = "https://openrouter.ai/api/v1",
            EnvKey = "OPENROUTER_API_KEY"
        },
        new ProviderPreset
        {
            DisplayName = "DeepSeek",
            ProviderId = "deepseek",
            ProviderName = "DeepSeek",
            BaseUrl = "https://api.deepseek.com",
            EnvKey = "DEEPSEEK_API_KEY"
        },
        new ProviderPreset
        {
            DisplayName = "Groq",
            ProviderId = "groq",
            ProviderName = "Groq",
            BaseUrl = "https://api.groq.com/openai/v1",
            EnvKey = "GROQ_API_KEY"
        },
        new ProviderPreset
        {
            DisplayName = "Together AI",
            ProviderId = "together-ai",
            ProviderName = "Together AI",
            BaseUrl = "https://api.together.ai/v1",
            EnvKey = "TOGETHER_API_KEY"
        },
        new ProviderPreset
        {
            DisplayName = "xAI",
            ProviderId = "xai",
            ProviderName = "xAI",
            BaseUrl = "https://api.x.ai/v1",
            EnvKey = "XAI_API_KEY"
        },
        new ProviderPreset
        {
            DisplayName = "Perplexity",
            ProviderId = "perplexity",
            ProviderName = "Perplexity",
            BaseUrl = "https://api.perplexity.ai/v1",
            EnvKey = "PERPLEXITY_API_KEY"
        },
        new ProviderPreset
        {
            DisplayName = "Cerebras",
            ProviderId = "cerebras",
            ProviderName = "Cerebras",
            BaseUrl = "https://api.cerebras.ai/v1",
            EnvKey = "CEREBRAS_API_KEY"
        },
        new ProviderPreset
        {
            DisplayName = "Fireworks AI",
            ProviderId = "fireworks-ai",
            ProviderName = "Fireworks AI",
            BaseUrl = "https://api.fireworks.ai/inference/v1",
            EnvKey = "FIREWORKS_API_KEY"
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
