using BPFL.API.Services;
using BPFL.API.Services.FantasyServices;
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
        private readonly ApiSportsPlayerSeedService playerSeedService;

        public AdminSyncController(TeamSyncService _teamSyncService, MatchSyncService _matchSyncService,
            PredictionScoringService _predictionScoringService,
            ApiSportsPlayerSeedService _playerSeedService)
        {
            teamSyncService = _teamSyncService;
            matchSyncService = _matchSyncService;
            predictionScoringService = _predictionScoringService;
            playerSeedService = _playerSeedService;
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
                return BadRequest("Valid matchId is required.");

            var result = await predictionScoringService.ScoreMatchPredictionsAsync(matchId);
            return Ok(result);
        }

        /// <summary>
        /// Seed fantasy players from api-sports.io for a given league and season.
        /// league: PL, PD, SA, BL1, FL1, CL
        /// season: e.g. 2024
        /// </summary>
        [HttpPost("seed-players")]
        public async Task<IActionResult> SeedPlayers(
            [FromQuery] string league = "PL",
            [FromQuery] int season = 2024,
            CancellationToken ct = default)
        {
            var result = await playerSeedService.SeedLeagueAsync(league, season, ct);
            return result.Success ? Ok(new { result.Message }) : BadRequest(new { result.Message });
        }
    }
}
