using BPFL.API.Modules.AI.Application.DTOs;

namespace BPFL.API.Modules.AI.Application.Interfaces
{
    public interface ICardsAgent
    {
        public interface ICardsAgent
        {
            Task<CardsAgentResponse> AnalyzeAsync(AgentFactsInput input, CancellationToken ct = default);
        }
    }
}
