using BPFL.API.Data;
using BPFL.API.Modules.Odds.Application.Interfaces;
using BPFL.API.Modules.Odds.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BPFL.API.Modules.Odds.Application.Repositories
{
    public class MatchMarketOddsRepository : IMatchMarketOddsRepository
    {

        private readonly BPFL_DBContext bPFL_DBContext;

        public MatchMarketOddsRepository(BPFL_DBContext _bPFL_DBContext)
        {
            bPFL_DBContext = _bPFL_DBContext;
        }
        public async Task AddAsync(MatchMarketOdds odds, CancellationToken ct = default)
        {
           await bPFL_DBContext.MatchMarketOdds.AddAsync(odds, ct);
        }

        public async Task AddRangeAsync(IEnumerable<MatchMarketOdds> odds, CancellationToken ct = default)
        {
           await bPFL_DBContext.MatchMarketOdds.AddRangeAsync(odds, ct);
        }

        public async Task<List<MatchMarketOdds>> GetByMatchIdAsync(int matchId, CancellationToken ct = default)
        {
            return await bPFL_DBContext.MatchMarketOdds.AsNoTracking()
                .Where(x => x.MatchId == matchId)
                .OrderBy(x => x.MarketCode)
                .ThenBy(x => x.SelectionCode)
                .ToListAsync(ct);
        }

        public async Task<MatchMarketOdds?> GetExactAsync(int matchId, string marketCode, string selectionCode, int? playerId, decimal? lineValue, CancellationToken ct = default)
        {
            return await bPFL_DBContext.MatchMarketOdds
                .FirstOrDefaultAsync(x =>
                    x.MatchId == matchId &&
                    x.MarketCode == marketCode &&
                    x.SelectionCode == selectionCode &&
                    x.PlayerId == playerId &&
                    x.LineValue == lineValue,
                    ct);
        }

        public async Task SaveChangesAsync(CancellationToken ct = default)
        {
            await bPFL_DBContext.SaveChangesAsync(ct);
        }
    }
}
