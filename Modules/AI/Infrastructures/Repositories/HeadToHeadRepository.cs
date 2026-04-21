using BPFL.API.Data;
using BPFL.API.Modules.AI.Application.DTOs;
using BPFL.API.Modules.AI.Application.Interfaces;
using BPFL.API.Modules.AI.Applications.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BPFL.API.Modules.AI.Infrastructures.Repositories
{
    public class HeadToHeadRepository : IHeadToHeadRepository
    {
        private readonly BPFL_DBContext bPFL_DBContext;

        public HeadToHeadRepository(BPFL_DBContext _bPFL_DBContext)
        {
            bPFL_DBContext = _bPFL_DBContext;
        }

        public async Task<List<HeadToHeadMatchResponse>> GetRecentAsync(
            int homeTeamId,
            int awayTeamId,
            CancellationToken ct = default)
        {
            return await bPFL_DBContext.Matches
                .AsNoTracking()
                .Where(m =>
                    m.Status == "FINISHED" &&
                    m.HomeScore != null &&
                    m.AwayScore != null &&
                    (
                        (m.HomeTeamId == homeTeamId && m.AwayTeamId == awayTeamId) ||
                        (m.HomeTeamId == awayTeamId && m.AwayTeamId == homeTeamId)
                    ))
                .OrderByDescending(m => m.MatchDate)
                .Take(5)
                .Select(m => new HeadToHeadMatchResponse
                {
                    MatchId = m.Id,
                    MatchDate = m.MatchDate,
                    HomeTeamId = m.HomeTeamId,
                    AwayTeamId = m.AwayTeamId,
                    HomeScore = m.HomeScore!.Value,
                    AwayScore = m.AwayScore!.Value
                })
                .ToListAsync(ct);
        }
    }
}