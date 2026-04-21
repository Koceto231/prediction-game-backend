using BPFL.API.Modules.AI.Application.DTOs;


namespace BPFL.API.Modules.AI.Applications.Interfaces
{
    public interface IHeadToHeadRepository
    {
        Task<List<HeadToHeadMatchResponse>> GetRecentAsync(
            int homeTeamId,
            int awayTeamId,
            CancellationToken ct = default);
    }
}
