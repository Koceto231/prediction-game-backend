using BPFL.API.Modules.AI.Application.DTOs;

namespace BPFL.API.Modules.AI.Application.Interfaces
{
    public interface ICornersAgent
    {
        public interface ICornersAgent
        {
            Task<CornersAgentResponse> AnalyzeAsync(AgentFactsInput input, CancellationToken ct = default);
        }
    }
}
