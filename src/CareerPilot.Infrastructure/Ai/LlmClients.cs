using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CareerPilot.Application.Ai;

namespace CareerPilot.Infrastructure.Ai;

internal static class LlmHttp
{
    public static async Task<JsonDocument> SendAsync(HttpClient http, HttpRequestMessage request, CancellationToken ct)
    {
        try
        {
            using var response = await http.SendAsync(request, ct);
            // Never surface the provider's body: it can echo prompts or contain account details.
            if (!response.IsSuccessStatusCode)
                throw new AiUnavailableException(response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden
                    ? "The AI provider rejected the credentials. Check Llm__ApiKey."
                    : $"The AI provider returned an error (HTTP {(int)response.StatusCode}).");
            return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        }
        catch (HttpRequestException)
        {
            throw new AiUnavailableException("The AI provider could not be reached.");
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new AiUnavailableException("The AI provider took too long to answer.");
        }
        catch (JsonException)
        {
            throw new AiUnavailableException("The AI provider returned an unreadable answer.");
        }
    }

    public static StringContent Body(object payload) =>
        new(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

    public static int Int(JsonElement parent, string obj, string prop) =>
        parent.TryGetProperty(obj, out var o) && o.ValueKind == JsonValueKind.Object
        && o.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n) ? n : 0;
}

public sealed class AnthropicLlmClient(HttpClient http, LlmOptions options) : ILlmClient
{
    public async Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken ct)
    {
        var baseUrl = string.IsNullOrWhiteSpace(options.BaseUrl) ? "https://api.anthropic.com" : options.BaseUrl.TrimEnd('/');
        using var message = new HttpRequestMessage(HttpMethod.Post, baseUrl + "/v1/messages")
        {
            Content = LlmHttp.Body(new
            {
                model = options.Model,
                max_tokens = request.MaxOutputTokens,
                system = request.System,
                messages = new[] { new { role = "user", content = request.User } },
            }),
        };
        message.Headers.Add("x-api-key", options.ApiKey);
        message.Headers.Add("anthropic-version", "2023-06-01");

        using var doc = await LlmHttp.SendAsync(http, message, ct);
        var text = new StringBuilder();
        if (doc.RootElement.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array)
            foreach (var block in content.EnumerateArray())
                if (block.TryGetProperty("type", out var t) && t.GetString() == "text" && block.TryGetProperty("text", out var tx))
                    text.Append(tx.GetString());
        return new LlmResponse(text.ToString(), LlmHttp.Int(doc.RootElement, "usage", "input_tokens"), LlmHttp.Int(doc.RootElement, "usage", "output_tokens"));
    }
}

/// <summary>Chat-completions style API: OpenAI-compatible hosts and local servers (Ollama, LM Studio, vLLM).</summary>
public sealed class OpenAiCompatibleLlmClient(HttpClient http, LlmOptions options) : ILlmClient
{
    public async Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken ct)
    {
        var baseUrl = string.IsNullOrWhiteSpace(options.BaseUrl) ? "http://localhost:11434/v1" : options.BaseUrl.TrimEnd('/');
        using var message = new HttpRequestMessage(HttpMethod.Post, baseUrl + "/chat/completions")
        {
            Content = LlmHttp.Body(new
            {
                model = options.Model,
                max_tokens = request.MaxOutputTokens,
                temperature = 0,
                messages = new object[]
                {
                    new { role = "system", content = request.System },
                    new { role = "user", content = request.User },
                },
            }),
        };
        if (!string.IsNullOrWhiteSpace(options.ApiKey))
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);

        using var doc = await LlmHttp.SendAsync(http, message, ct);
        var text = "";
        if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array && choices.GetArrayLength() > 0
            && choices[0].TryGetProperty("message", out var m) && m.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.String)
            text = c.GetString() ?? "";
        return new LlmResponse(text, LlmHttp.Int(doc.RootElement, "usage", "prompt_tokens"), LlmHttp.Int(doc.RootElement, "usage", "completion_tokens"));
    }
}
