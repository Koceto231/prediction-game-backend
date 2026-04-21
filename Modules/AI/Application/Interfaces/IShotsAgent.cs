using BPFL.API.Modules.AI.Application.DTOs;

namespace BPFL.API.Modules.AI.Application.Interfaces
{
    public interface IShotsAgent
    {
        Task<ShotsAgentResponse> AnalyzeAsync(AgentFactsInput input, CancellationToken ct = default);
    }
}
