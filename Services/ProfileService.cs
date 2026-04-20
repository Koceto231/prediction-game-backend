using BPFL.API.Data;
using BPFL.API.Models.DTO;
using Microsoft.EntityFrameworkCore;

namespace BPFL.API.Services
{
    public class ProfileService
    {

        private readonly BPFL_DBContext bPFL_DBContext;
        private readonly ILogger<ProfileService> logger;

        public ProfileService(BPFL_DBContext _bpfl_DBContext, ILogger<ProfileService> _logger)
        {
            bPFL_DBContext = _bpfl_DBContext;
            logger = _logger;
        }

        public async Task<ProfileStatsDTO> GetStatsAsync(int userId, CancellationToken ct =default )
        {
            var stats = await bPFL_DBContext.Predictions
                .AsNoTracking()
                .Where(p => p.UserId == userId)
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    TotalPredictions = g.Count(),
                    TotalPoints = g.Sum(p => p.Points),
                    CorrectOutcomeCount = g.Count(p => p.Points >= 1)
                })
                .FirstOrDefaultAsync(ct);

            if (stats == null)
            {
                return new ProfileStatsDTO
                {
                    TotalPredictions = 0,
                    TotalPoints = 0,
                    CorrectOutcomeCount = 0,
                    AccuracyPercent = 0
                };
            }

            var accuracyPercent = stats.TotalPredictions == 0
                ? 0
                : Math.Round((double)stats.CorrectOutcomeCount / stats.TotalPredictions * 100, 2);

            return new ProfileStatsDTO
            {
                TotalPredictions = stats.TotalPredictions,
                TotalPoints = stats.TotalPoints ?? 0,
                CorrectOutcomeCount = stats.CorrectOutcomeCount,
                AccuracyPercent = accuracyPercent
            };

        }

        public async Task<UserResponseDTO> GetCurrentProfileAsync(int userId, CancellationToken ct = default)
        {
            var user = await bPFL_DBContext.Users
                 .AsNoTracking()
                 .Where(u => u.Id == userId)
                 .Select(u => new UserResponseDTO
                 {
                     Id = u.Id,
                     Email = u.Email,
                     Username = u.Username,
                     Role = u.Role,
                     Balance = u.Balance
                 })
                 .FirstOrDefaultAsync(ct);

            if (user == null)
                throw new KeyNotFoundException("User not found.");

            return user;
        }
    }
}
