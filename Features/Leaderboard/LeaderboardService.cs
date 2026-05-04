using BPFL.API.Data;
using BPFL.API.Features.Leaderboard;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace BPFL.API.Features.Leaderboard
{
    public class LeaderboardService
    {
        private readonly BPFL_DBContext bPFL_DBContext;
        private readonly IMemoryCache cache;


        private const string GlobalLeaderboardKey = "leaderboard:global";


        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(2);

        public LeaderboardService(BPFL_DBContext _bPFL_DBContext, IMemoryCache _cache)
        {
            bPFL_DBContext = _bPFL_DBContext;
            cache = _cache;

        }

        public async Task<List<LeaderboardResponseDTO>> GetLeaderboardsAsync(CancellationToken ct = default)
        {

            if (cache.TryGetValue(GlobalLeaderboardKey, out List<LeaderboardResponseDTO>? cached) && cached != null)
                return cached;

            var predictions = await bPFL_DBContext.Predictions
                .AsNoTracking()
                .GroupBy(x => x.UserId)
                .Select(k => new LeaderboardResponseDTO
                {
                    UserId = k.Key,
                    Username = k.First().User.Username,
                    TotalPoints = k.Sum(l => l.Points ?? 0),
                    CorrectResults = k.Count(p => p.Points == 3)
                })
                .OrderByDescending(x => x.TotalPoints)
                .ThenByDescending(x => x.CorrectResults)
                .ToListAsync(ct);

            cache.Set(GlobalLeaderboardKey, predictions, CacheDuration);

            return predictions;
        }

        public void InvalidateLeaderboardCache()
        {
            cache.Remove(GlobalLeaderboardKey);
        }
    }
}
