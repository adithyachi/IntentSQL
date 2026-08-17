namespace BizPulse.AI.POC.Models;

public class SqlGenerationResult
{
    public string Sql { get; init; } = string.Empty;

    public AiGenerationResult AiGeneration { get; init; } = null!;
}