using BPFL.API.Modules.Odds.Domain.Entities;

namespace BPFL.API.Modules.Odds.Application.Interfaces
{
    public interface IMatchMarketOddsRepository
    {
        Task<List<MatchMarketOdds>> GetByMatchIdAsync(int matchId, CancellationToken ct = default);

        Task<MatchMarketOdds?> GetExactAsync(
            int matchId,
            string marketCode,
            string selectionCode,
            int? playerId,
            decimal? lineValue,
            CancellationToken ct = default);

        Task AddAsync(MatchMarketOdds odds, CancellationToken ct = default);

        Task AddRangeAsync(IEnumerable<MatchMarketOdds> odds, CancellationToken ct = default);

        Task SaveChangesAsync(CancellationToken ct = default);
    }
}
