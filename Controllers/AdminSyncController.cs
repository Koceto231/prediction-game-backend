using BPFL.API.Services;
using BPFL.API.Services.FantasyServices;
using BPFL.API.Services.MatchServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BPFL.API.Controllers
{
    [Authorize(Roles = "Admin")]
    [ApiController]
    [Route("api/admin/sync")]
    public class AdminSyncController : ControllerBase
    {
        private readonly TeamSyncService _teams;
        private readonly MatchSyncService _matches;
        private readonly PredictionScoringService _scoring;
        private readonly SportmonksMatchSyncService _sportmonks;
        private readonly FantasyAutoSyncService _fantasySync;

        public AdminSyncController(
            TeamSyncService teams,
            MatchSyncService matches,
            PredictionScoringService scoring,
            SportmonksMatchSyncService sportmonks,
            FantasyAutoSyncService fantasySync)
        {
            _teams       = teams;
            _matches     = matches;
            _scoring     = scoring;
            _sportmonks  = sportmonks;
            _fantasySync = fantasySync;
        }

        // ── football-data.org ─────────────────────────────────────────

        [HttpPost("teams")]
        public async Task<IActionResult> ImportTeams([FromQuery] string competitionIdOrCode)
        {
            if (string.IsNullOrWhiteSpace(competitionIdOrCode))
                return BadRequest("competitionIdOrCode is required");
            return Ok(await _teams.ImportTeamAsync(competitionIdOrCode));
        }

        [HttpPost("matches")]
        public async Task<IActionResult> ImportMatches([FromQuery] string competitionIdOrCode)
        {
            if (string.IsNullOrWhiteSpace(competitionIdOrCode))
                return BadRequest("competitionIdOrCode is required");
            return Ok(await _matches.ImportMatchesAsync(competitionIdOrCode));
        }

        // ── Sportmonks ────────────────────────────────────────────────

        /// <summary>
        /// Sync matches from Sportmonks for the given league code.
        /// leagueCode: BGL (efbet Liga), PL, BL1, SA, PD
        /// daysAhead: how many days forward to import (default 30)
        /// </summary>
        [HttpPost("matches/sportmonks")]
        public async Task<IActionResult> ImportSportmonksMatches(
            [FromQuery] string leagueCode = "BGL",
            [FromQuery] int daysAhead = 30,
            CancellationToken ct = default)
        {
            try
            {
                var (added, updated) = await _sportmonks.SyncLeagueMatchesAsync(leagueCode, daysAhead, ct);
                return Ok(new { added, updated, message = $"Sportmonks sync done for {leagueCode}." });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // ── Fantasy players ───────────────────────────────────────────

        /// <summary>
        /// Sync fantasy players from football-data.org squad data for all teams in DB.
        /// </summary>
        [HttpPost("sync-players")]
        public async Task<IActionResult> SyncPlayers(CancellationToken ct = default)
        {
            await _fantasySync.SyncPlayersFromSquadsAsync([], ct);
            return Ok(new { message = "Squad sync triggered. Check logs for progress." });
        }

        // ── Scoring ───────────────────────────────────────────────────

        [HttpPost("score/predictions/{matchId}")]
        public async Task<IActionResult> ScoreMatchPredictions([FromRoute] int matchId)
        {
            if (matchId <= 0) return BadRequest("Valid matchId is required.");
            return Ok(await _scoring.ScoreMatchPredictionsAsync(matchId));
        }
    }
}
