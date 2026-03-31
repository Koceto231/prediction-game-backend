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
            var predictions = await bPFL_DBContext.Predictions.AsNoTracking().Where(p => p.UserId == userId).ToListAsync(ct);

            var totalPredictions = predictions.Count;
            var totalPoints = predictions.Sum(p => p.Points);
            var correctOutcomeCount = predictions.Count(p => p.Points >= 1);

            var accuracyPercent = totalPredictions == 0
           ? 0
           : Math.Round((double)correctOutcomeCount / totalPredictions * 100, 2);

            return new ProfileStatsDTO
            {
                TotalPredictions = totalPredictions,
                TotalPoints = totalPoints,
                CorrectOutcomeCount = correctOutcomeCount,
                AccuracyPercent = accuracyPercent
            };

        }

        public async Task<UserResponseDTO> GetCurrentProfileAsync(int userId, CancellationToken ct = default)
        {
            var user = await bPFL_DBContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId, ct);

            if (user == null)
            {
                throw new KeyNotFoundException("User not found.");
            }

            return new UserResponseDTO
            {
                Id = user.Id,
                Email = user.Email,
                Username = user.Username,
                Role = user.Role
            };
        }
    }
}
