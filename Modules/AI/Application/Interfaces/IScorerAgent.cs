using BPFL.API.Modules.AI.Application.DTOs;

namespace BPFL.API.Modules.AI.Application.Interfaces
{
    public interface IScorerAgent
    {
        Task<ScorerAgentResponse> AnalyzeAsync(AgentFactsInput input, CancellationToken ct = default);
    }
}
