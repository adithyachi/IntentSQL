namespace BizPulse.AI.POC.Models;

public class AiAgentAttempt
{
    public long Id { get; set; }

    public long AiAgentExecutionId { get; set; }

    public int AttemptNumber { get; set; }

    public string Sql { get; set; } = string.Empty;

    public bool Success { get; set; }

    public string? Error { get; set; }

    public long GenerationTimeMs { get; set; }

    public DateTime CreatedAt { get; set; }

    public AiAgentExecution AiAgentExecution { get; set; }
        = null!;
}