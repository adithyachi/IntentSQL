using BizPulse.AI.POC.Models;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace BizPulse.AI.POC.Services;

public class TogetherAiService : IAiTextGenerationService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public TogetherAiService(
        HttpClient httpClient,
        IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public async Task<AiGenerationResult> GenerateAsync(
        string prompt,
        bool thinkEnabled = false,
        CancellationToken cancellationToken = default)
    {
        var apiKey =
            _configuration["TogetherAI:ApiKey"];

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "Together AI API key is not configured.");
        }

        var model =
            _configuration["TogetherAI:Model"]
            ?? "Qwen/Qwen3.7-Max";

        var requestBody = new
        {
            model = model,

            messages = new[]
            {
                new
                {
                    role = "user",
                    content = prompt
                }
            },

            max_tokens = 2000,

            temperature = 0,

            stream = true,

            enable_thinking = thinkEnabled
        };

        var json =
            JsonSerializer.Serialize(requestBody);

        using var request =
            new HttpRequestMessage(
                HttpMethod.Post,
                "v1/chat/completions");

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                apiKey);

        request.Content =
            new StringContent(
                json,
                Encoding.UTF8,
                "application/json");

        var stopwatch =
            Stopwatch.StartNew();

        using var response =
            await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

        response.EnsureSuccessStatusCode();

        var contentBuilder =
            new StringBuilder();

        var inputTokens = 0;
        var outputTokens = 0;
        var totalTokens = 0;
        var reasoningTokens = 0;
        var reasoningBuilder = new StringBuilder();

        await using var stream =
            await response.Content.ReadAsStreamAsync(
                cancellationToken);

        using var reader =
            new StreamReader(stream);

        while (!reader.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var line =
                await reader.ReadLineAsync(
                    cancellationToken);

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (!line.StartsWith("data: "))
            {
                continue;
            }

            var data =
                line["data: ".Length..].Trim();

            if (data == "[DONE]")
            {
                break;
            }

            try
            {
                using var document =
                    JsonDocument.Parse(data);

                var root =
                    document.RootElement;

                // -----------------------------------------------------
                // Generated content
                // -----------------------------------------------------

                if (root.TryGetProperty(
                        "choices",
                        out var choices) &&
                    choices.GetArrayLength() > 0)
                {
                    var choice =
                        choices[0];

                    if (choice.TryGetProperty(
                            "delta",
                            out var delta))
                    {
                        // Qwen thinking content may be exposed as
                        // reasoning_content. Together's reasoning API can
                        // also expose it as reasoning, so accept both.
                        if (delta.TryGetProperty(
                                "reasoning_content",
                                out var reasoningContent) &&
                            reasoningContent.ValueKind ==
                            JsonValueKind.String)
                        {
                            reasoningBuilder.Append(
                                reasoningContent.GetString());
                        }
                        else if (delta.TryGetProperty(
                                     "reasoning",
                                     out var reasoning) &&
                                 reasoning.ValueKind ==
                                 JsonValueKind.String)
                        {
                            reasoningBuilder.Append(
                                reasoning.GetString());
                        }

                        if (delta.TryGetProperty(
                                "content",
                                out var contentElement) &&
                            contentElement.ValueKind ==
                            JsonValueKind.String)
                        {
                            contentBuilder.Append(
                                contentElement.GetString());
                        }
                    }
                }

                // -----------------------------------------------------
                // Usage information
                // Together normally sends this in the final chunk.
                // -----------------------------------------------------

                if (root.TryGetProperty(
                        "usage",
                        out var usage) &&
                    usage.ValueKind !=
                    JsonValueKind.Null)
                {
                    if (usage.TryGetProperty(
                            "prompt_tokens",
                            out var promptTokens))
                    {
                        inputTokens =
                            promptTokens.GetInt32();
                    }

                    if (usage.TryGetProperty(
                            "completion_tokens",
                            out var completionTokens))
                    {
                        outputTokens =
                            completionTokens.GetInt32();
                    }

                    if (usage.TryGetProperty(
                            "total_tokens",
                            out var total))
                    {
                        totalTokens =
                            total.GetInt32();
                    }

                    if (usage.TryGetProperty(
                            "completion_tokens_details",
                            out var completionDetails))
                    {
                        if (completionDetails.TryGetProperty(
                                "reasoning_tokens",
                                out var reasoning))
                        {
                            reasoningTokens =
                                reasoning.GetInt32();
                        }
                    }
                }
            }
            catch (JsonException)
            {
                // Ignore malformed/non-JSON streaming lines.
                // The API response may contain SSE lines that are
                // not JSON payloads.
            }
        }

        stopwatch.Stop();

        var content = contentBuilder.ToString().Trim();
        var reasoningText = reasoningBuilder.ToString().Trim();

        // Some Qwen/reasoning deployments embed reasoning in <think> tags.
        if (thinkEnabled && string.IsNullOrWhiteSpace(reasoningText))
        {
            var match = System.Text.RegularExpressions.Regex.Match(
                content,
                @"<think>\s*(.*?)\s*</think>",
                System.Text.RegularExpressions.RegexOptions.Singleline |
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (match.Success)
            {
                reasoningText = match.Groups[1].Value.Trim();
                content = System.Text.RegularExpressions.Regex.Replace(
                    content,
                    @"<think>.*?</think>\s*",
                    string.Empty,
                    System.Text.RegularExpressions.RegexOptions.Singleline |
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();
            }
        }

        return new AiGenerationResult
        {
            Content = content,

            Reasoning =
                thinkEnabled && !string.IsNullOrWhiteSpace(reasoningText)
                    ? reasoningText
                    : null,

            Provider =
                "TogetherAI",

            Model =
                model,

            InputTokens =
                inputTokens,

            OutputTokens =
                outputTokens,

            TotalTokens =
                totalTokens,

            ReasoningTokens =
                reasoningTokens,

            ResponseTimeMs =
                stopwatch.ElapsedMilliseconds
        };
    }
}