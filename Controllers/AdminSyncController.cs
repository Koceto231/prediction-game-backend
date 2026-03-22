using BPFL.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BPFL.API.Controllers
{
    [Authorize(Roles = "Admin")]
    [ApiController]
    [Route("api/admin/sync")]
    public class AdminSyncController : ControllerBase
    {
        private readonly TeamSyncService teamSyncService;
        private readonly MatchSyncService matchSyncService;
        private readonly PredictionScoringService predictionScoringService;

        public AdminSyncController(TeamSyncService _teamSyncService, MatchSyncService _matchSyncService, 
            PredictionScoringService _predictionScoringService)
        {
            teamSyncService = _teamSyncService;
            matchSyncService = _matchSyncService;
            predictionScoringService = _predictionScoringService;
        }

        [HttpPost("teams")]
        public async Task<IActionResult> ImportTeams([FromQuery] string competitionIdOrCode)
        {
            if (string.IsNullOrWhiteSpace(competitionIdOrCode))
            {
                return BadRequest("competitionIdOrCode is required");
            }
            var result = await teamSyncService.ImportTeamAsync(competitionIdOrCode);

            return Ok(result);
        }


        [HttpPost("matches")]
        public async Task<IActionResult> ImportMatches([FromQuery] string competitionIdOrCode)
        {
            if (string.IsNullOrWhiteSpace(competitionIdOrCode))
            {
                return BadRequest("competitionIdOrCode is required");
            }
            var result = await matchSyncService.ImportMatchesAsync(competitionIdOrCode);

            return Ok(result);
        }

        [HttpPost("score/predictions/{matchId}")]
        public async Task<IActionResult> ScoreMatchPredictions([FromRoute] int matchId)
        {
            if (matchId <= 0)
            {
                return BadRequest("Valid matchId is required.");
            }

            var result = await predictionScoringService.ScoreMatchPredictionsAsync(matchId);
            return Ok(result);
        }

    }
}
