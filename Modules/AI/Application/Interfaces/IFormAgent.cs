using BPFL.API.Modules.AI.Application.DTOs;


namespace BPFL.API.Modules.AI.Applications.Interfaces
{
    public interface IFormAgent
    {
        Task<FormAgentResponse> AnalyzeAsync(
            AgentFactsInput input,
            CancellationToken ct = default);
    }
}