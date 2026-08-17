using BizPulse.AI.POC.Models;

namespace BizPulse.AI.POC.Services;

public interface IAiTextGenerationService
{
    Task<AiGenerationResult> GenerateAsync(
        string prompt,
        CancellationToken cancellationToken = default);
}