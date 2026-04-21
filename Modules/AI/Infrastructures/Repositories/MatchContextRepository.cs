using BPFL.API.Data;
using BPFL.API.Modules.AI.Application.DTOs;
using BPFL.API.Modules.AI.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BPFL.API.Modules.AI.Infrastructures.Repositories
{
    public class MatchContextRepository
         : IMatchContextRepository
    {
        
        private readonly BPFL_DBContext bPFL_DBContext;

        public MatchContextRepository(BPFL_DBContext _bPFL_DBContext)
        {
            bPFL_DBContext = _bPFL_DBContext;
        }
        public async Task<MatchContextResponse> GetByMatchIdAsync(int matchId, CancellationToken ct = default)
        {
            if (matchId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(matchId));
            }

            var match = await bPFL_DBContext.Matches.Include(m => m.HomeTeam).Include(k => k.AwayTeam)
                .FirstOrDefaultAsync(m => m.Id == matchId, ct);

            if (match == null)
            {
                throw new KeyNotFoundException($"Match with id {matchId} was not found.");
            }

            return new MatchContextResponse
            {
                MatchId = match.Id,
                MatchDate = match.MatchDate,
                Status = match.Status ?? string.Empty,
                MatchDay = match.MatchDay,

                HomeTeamId = match.HomeTeamId,
                HomeTeamName = match.HomeTeam?.Name ?? "Home",

                AwayTeamId = match.AwayTeamId,
                AwayTeamName = match.AwayTeam?.Name ?? "Away",

                HomeScore = match.HomeScore,
                AwayScore = match.AwayScore
            };
        }
    }
}
