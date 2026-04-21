using BPFL.API.Modules.AI.Application.DTOs;

namespace BPFL.API.Modules.AI.Application.Interfaces
{
    public interface IGoalsAgent
    {
        public interface IGoalsAgent
        {
            Task<GoalsAgentResponse> AnalyzeAsync(AgentFactsInput input, CancellationToken ct = default);
        }
    }
}
