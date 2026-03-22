using BPFL.API.Data;
using BPFL.API.Models.DTO;
using Microsoft.EntityFrameworkCore;

namespace BPFL.API.Services
{
    public class LeaderboardService
    {
        private readonly BPFL_DBContext bPFL_DBContext;
        

        public LeaderboardService(BPFL_DBContext _bPFL_DBContext)
        {
            bPFL_DBContext = _bPFL_DBContext;
           
        }

        public async Task<List<LeaderboardResponseDTO>> GetLeaderboardsAsync(CancellationToken ct = default)
        {
            var predictions = await bPFL_DBContext.Predictions.AsNoTracking().GroupBy(x => x.UserId)
                .Select(k => new LeaderboardResponseDTO
                {
                    UserId = k.Key,
                    Username = k.First().User.Username,
                    TotalPoints = k.Sum(l => l.Points),
                    CorrectResults = k.Count(p => p.Points == 3)

                }).OrderByDescending(x => x.TotalPoints)
                .ThenByDescending(x => x.CorrectResults)
                .ToListAsync(ct);

            return predictions;
        }
    }
}
