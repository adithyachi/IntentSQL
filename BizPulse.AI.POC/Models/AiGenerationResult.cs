namespace BizPulse.AI.POC.Models;

public class AiGenerationResult
{
    public string Content { get; init; } = string.Empty;

    public string? Reasoning { get; init; }

    public string Provider { get; init; } = string.Empty;

    public string Model { get; init; } = string.Empty;

    public int InputTokens { get; init; }

    public int OutputTokens { get; init; }

    public int TotalTokens { get; init; }

    public int ReasoningTokens { get; init; }

    public long ResponseTimeMs { get; init; }
}