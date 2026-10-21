namespace CareerPilot.Application.Ai;

public enum LlmProvider { None, Anthropic, OpenAiCompatible }

/// <summary>
/// LLM settings (appsettings section "Llm"). Off by default. Put the key in the environment
/// (Llm__ApiKey), never in a committed file. OpenAiCompatible also covers local servers such as Ollama.
/// </summary>
public sealed class LlmOptions
{
    public const string Section = "Llm";

    public LlmProvider Provider { get; set; } = LlmProvider.None;
    public string Model { get; set; } = "";
    public string? ApiKey { get; set; }

    /// <summary>Empty means the provider default (Anthropic API, or http://localhost:11434/v1 for OpenAiCompatible).</summary>
    public string? BaseUrl { get; set; }

    public int MaxOutputTokens { get; set; } = 800;

    /// <summary>The posting text sent to the model is cut at this many characters (cost control).</summary>
    public int MaxPostingChars { get; set; } = 6000;

    /// <summary>Hard cap on new analyses per UTC day. Cached analyses are free.</summary>
    public int DailyCallLimit { get; set; } = 20;

    public int TimeoutSeconds { get; set; } = 60;

    public bool IsConfigured =>
        Provider != LlmProvider.None
        && !string.IsNullOrWhiteSpace(Model)
        && (Provider != LlmProvider.Anthropic || !string.IsNullOrWhiteSpace(ApiKey));
}

public sealed record LlmRequest(string System, string User, int MaxOutputTokens);

public sealed record LlmResponse(string Text, int InputTokens, int OutputTokens);

/// <summary>Thrown with a message that is safe to show the owner (never contains provider bodies or keys).</summary>
public sealed class AiUnavailableException(string message) : Exception(message);

/// <summary>A model that can only produce text. It is given no tools, so it cannot act on anything.</summary>
public interface ILlmClient
{
    Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken ct);
}

public sealed class DisabledLlmClient : ILlmClient
{
    public const string NotConfigured = "AI analysis is not configured. Set Llm__Provider, Llm__Model and (for Anthropic) Llm__ApiKey.";

    public Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken ct) =>
        throw new AiUnavailableException(NotConfigured);
}
