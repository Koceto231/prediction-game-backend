using BPFL.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace BPFL.API.Controllers
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
