using BizPulse.AI.POC.Models;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace BizPulse.AI.POC.Services;

public class OllamaService : IAiTextGenerationService
{
    private readonly HttpClient _httpClient;

    public OllamaService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<AiGenerationResult> GenerateAsync(
        string prompt,
        CancellationToken cancellationToken = default)
    {
        var request = new OllamaRequest
        {
            Model = "qwen3:1.7b",
            Prompt = prompt,
            Stream = false,
            Think = false,

            Options = new OllamaOptions
            {
                Temperature = 0,
                TopK = 20,
                TopP = 0.95,
                Seed = 42,

                // Our schema + prompt can be fairly large.
                NumCtx = 8192
            }
        };

        var stopwatch =
            Stopwatch.StartNew();

        var response =
            await _httpClient.PostAsJsonAsync(
                "api/generate",
                request,
                cancellationToken);

        response.EnsureSuccessStatusCode();

        var result =
            await response.Content
                .ReadFromJsonAsync<OllamaResponse>(
                    cancellationToken);

        stopwatch.Stop();

        if (result == null)
        {
            throw new InvalidOperationException(
                "Ollama returned an empty response.");
        }

        return new AiGenerationResult
        {
            Content =
                result.Response ?? string.Empty,

            Provider =
                "Ollama",

            Model =
                result.Model ?? "qwen3:1.7b",

            InputTokens =
                result.PromptEvalCount,

            OutputTokens =
                result.EvalCount,

            TotalTokens =
                result.PromptEvalCount +
                result.EvalCount,

            // Ollama's generate response does not provide
            // reasoning-token usage in the same form as Together AI.
            ReasoningTokens =
                0,

            ResponseTimeMs =
                stopwatch.ElapsedMilliseconds
        };
    }

    private class OllamaRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("prompt")]
        public string Prompt { get; set; } = string.Empty;

        [JsonPropertyName("stream")]
        public bool Stream { get; set; }

        [JsonPropertyName("think")]
        public bool Think { get; set; }

        [JsonPropertyName("options")]
        public OllamaOptions? Options { get; set; }
    }

    private class OllamaOptions
    {
        [JsonPropertyName("temperature")]
        public double Temperature { get; set; }

        [JsonPropertyName("top_k")]
        public int TopK { get; set; }

        [JsonPropertyName("top_p")]
        public double TopP { get; set; }

        [JsonPropertyName("seed")]
        public int Seed { get; set; }

        [JsonPropertyName("num_ctx")]
        public int NumCtx { get; set; }
    }

    private class OllamaResponse
    {
        [JsonPropertyName("model")]
        public string? Model { get; set; }

        [JsonPropertyName("response")]
        public string? Response { get; set; }

        [JsonPropertyName("prompt_eval_count")]
        public int PromptEvalCount { get; set; }

        [JsonPropertyName("eval_count")]
        public int EvalCount { get; set; }

        [JsonPropertyName("total_duration")]
        public long TotalDuration { get; set; }

        [JsonPropertyName("load_duration")]
        public long LoadDuration { get; set; }

        [JsonPropertyName("prompt_eval_duration")]
        public long PromptEvalDuration { get; set; }

        [JsonPropertyName("eval_duration")]
        public long EvalDuration { get; set; }
    }
}