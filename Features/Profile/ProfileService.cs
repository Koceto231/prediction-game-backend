using BPFL.API.Data;
using BPFL.API.Features.Auth;
using BPFL.API.Shared;
using Microsoft.EntityFrameworkCore;

namespace BPFL.API.Features.Profile
{
    public class ProfileService
    {
        private readonly BPFL_DBContext bPFL_DBContext;
        private readonly ILogger<ProfileService> logger;
        private readonly IAppCache _cache;

        private static readonly TimeSpan StatsTtl = TimeSpan.FromMinutes(5);

        public ProfileService(BPFL_DBContext _bpfl_DBContext, ILogger<ProfileService> _logger, IAppCache cache)
        {
            bPFL_DBContext = _bpfl_DBContext;
            logger         = _logger;
            _cache         = cache;
        }

        public async Task<ProfileStatsDTO> GetStatsAsync(int userId, CancellationToken ct = default)
        {
            var cacheKey = $"profile:stats:{userId}";
            var cached   = await _cache.GetAsync<ProfileStatsDTO>(cacheKey, ct);
            if (cached != null) return cached;

            var stats = await bPFL_DBContext.Predictions
                .AsNoTracking()
                .Where(p => p.UserId == userId)
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    TotalPredictions    = g.Count(),
                    TotalPoints         = g.Sum(p => p.Points),
                    CorrectOutcomeCount = g.Count(p => p.Points >= 1)
                })
                .FirstOrDefaultAsync(ct);

            ProfileStatsDTO result;

            if (stats == null)
            {
                result = new ProfileStatsDTO
                {
                    TotalPredictions    = 0,
                    TotalPoints         = 0,
                    CorrectOutcomeCount = 0,
                    AccuracyPercent     = 0
                };
            }
            else
            {
                var accuracy = stats.TotalPredictions == 0
                    ? 0
                    : Math.Round((double)stats.CorrectOutcomeCount / stats.TotalPredictions * 100, 2);

                result = new ProfileStatsDTO
                {
                    TotalPredictions    = stats.TotalPredictions,
                    TotalPoints         = stats.TotalPoints ?? 0,
                    CorrectOutcomeCount = stats.CorrectOutcomeCount,
                    AccuracyPercent     = accuracy
                };
            }

            await _cache.SetAsync(cacheKey, result, StatsTtl, ct);
            return result;
        }

        public Task InvalidateStatsAsync(int userId, CancellationToken ct = default)
            => _cache.RemoveAsync($"profile:stats:{userId}", ct);

        public async Task<UserResponseDTO> GetCurrentProfileAsync(int userId, CancellationToken ct = default)
        {
            var user = await bPFL_DBContext.Users
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => new UserResponseDTO
                {
                    Id       = u.Id,
                    Email    = u.Email,
                    Username = u.Username,
                    Role     = u.Role,
                    Balance  = u.Balance
                })
                .FirstOrDefaultAsync(ct);

            if (user == null)
                throw new KeyNotFoundException("User not found.");

            return user;
        }
    }
}
