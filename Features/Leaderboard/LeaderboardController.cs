using BPFL.API.Features.Leaderboard;
using Microsoft.AspNetCore.Mvc;

namespace BPFL.API.Features.Leaderboard
{
    [ApiController]
    [Route("api/[controller]")]
    public class LeaderboardController : ControllerBase
    {

        private readonly LeaderboardService leaderboardService;

        public LeaderboardController(LeaderboardService _leaderboardService)
        {
            leaderboardService = _leaderboardService;
        }

        [HttpGet]
        public async Task<IActionResult> LeaderboardAsync()
        {
            var result = await leaderboardService.GetLeaderboardsAsync();
            return Ok(result);
        }
    }
}
