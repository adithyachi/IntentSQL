namespace BizPulse.AI.POC.Models;

public class SqlAttemptViewModel
{
    public int AttemptNumber { get; set; }

    public string Sql { get; set; } = string.Empty;

    public bool Success { get; set; }

    public string? Error { get; set; }

    public double GenerationTimeSeconds { get; set; }
}