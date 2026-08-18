using BizPulse.AI.POC.Models;

namespace BizPulse.AI.POC.Services;

public interface IAiTextGenerationService
{
    Task<AiGenerationResult> GenerateAsync(
        string prompt,
        bool thinkEnabled = false,
        CancellationToken cancellationToken = default);
}