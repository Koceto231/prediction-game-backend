using BPFL.API.Modules.AI.Application.DTOs;

namespace BPFL.API.Modules.AI.Application.Interfaces
{
    public interface IMatchContextRepository
    {
        Task<MatchContextResponse> GetByMatchIdAsync(int matchId, CancellationToken ct = default);
    }
}
