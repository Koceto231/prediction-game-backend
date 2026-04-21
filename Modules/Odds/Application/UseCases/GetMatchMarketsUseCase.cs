using BPFL.API.Modules.Odds.Application.DTOs;
using BPFL.API.Modules.Odds.Application.Interfaces;
using BPFL.API.Shared;

namespace BPFL.API.Modules.Odds.Application.UseCases
{
    public class GetMatchMarketsUseCase
    {
        private readonly IMatchMarketOddsRepository matchMarketOddsRepository;
        private readonly IAppCache appCache;

        public GetMatchMarketsUseCase(IMatchMarketOddsRepository _matchMarketOddsRepository, IAppCache _appCache)
        {
            matchMarketOddsRepository = _matchMarketOddsRepository;
            appCache = _appCache;
        }

        public async Task<List<MatchMarketOddsResponseDTO>> ExecuteAsync(int matchId,
            CancellationToken ct = default)
        {
            var cacheKey = $"match:markets:{matchId}";

            var cached = await appCache.GetAsync<List<MatchMarketOddsResponseDTO>>(cacheKey,ct);

            if (cached != null) 
                return cached;

            var odds = await matchMarketOddsRepository.GetByMatchIdAsync(matchId,ct);

            var result = odds.Select(x => new MatchMarketOddsResponseDTO
            {
                MarketCode = x.MarketCode,
                SelectionCode = x.SelectionCode,
                PlayerId = x.PlayerId,
                LineValue = x.LineValue,
                Odds = x.Odds,
                UpdatedAt = x.UpdatedAt
            }).ToList();

            await appCache.SetAsync(cacheKey, result ,TimeSpan.FromMinutes(2), ct);

            return result;
        }
    }
}
