namespace BizPulse.AI.POC.Models;

public class AiAgentExecution
{
    public long Id { get; set; }

    public string Question { get; set; } = string.Empty;

    public string Mode { get; set; } = string.Empty;

    public string? Response { get; set; }

    public bool ThinkEnabled { get; set; }

    public string? Reasoning { get; set; }

    public string Provider { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public int InputTokens { get; set; }

    public int OutputTokens { get; set; }

    public int TotalTokens { get; set; }

    public int ReasoningTokens { get; set; }

    public long ResponseTimeMs { get; set; }

    public long TotalProcessingTimeMs { get; set; }

    public bool Success { get; set; }

    public string? FinalSql { get; set; }

    public string? Error { get; set; }

    public DateTime CreatedAt { get; set; }

    public ICollection<AiAgentAttempt> Attempts { get; set; }
        = new List<AiAgentAttempt>();
}