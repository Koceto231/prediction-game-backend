using BPFL.API.Data;
using BPFL.API.Services;
using BPFL.API.Services.FantasyServices;
using BPFL.API.Services.MatchServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BPFL.API.Controllers
{
    [Authorize(Roles = "Admin")]
    [ApiController]
    [Route("api/admin/sync")]
    public class AdminSyncController : ControllerBase
    {
        private readonly PredictionScoringService _scoring;
        private readonly SportmonksMatchSyncService _sportmonks;
        private readonly FantasyAutoSyncService _fantasySync;
        private readonly BPFL_DBContext _db;

        public AdminSyncController(
            PredictionScoringService scoring,
            SportmonksMatchSyncService sportmonks,
            FantasyAutoSyncService fantasySync,
            BPFL_DBContext db)
        {
            _scoring     = scoring;
            _sportmonks  = sportmonks;
            _fantasySync = fantasySync;
            _db          = db;
        }

        // ── Sportmonks — matches ──────────────────────────────────────

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

        // ── Deduplicate matches ───────────────────────────────────────

        /// <summary>
        /// Remove duplicate matches — keeps the one with the lower Id (oldest import)
        /// and deletes any extra rows with the same HomeTeamId + AwayTeamId + same calendar day.
        /// </summary>
        [HttpPost("matches/dedup")]
        public async Task<IActionResult> DedupMatches(CancellationToken ct = default)
        {
            var all = await _db.Matches.OrderBy(m => m.Id).ToListAsync(ct);

            var seen  = new HashSet<string>();
            var toDelete = new List<BPFL.API.Models.Match>();

            foreach (var m in all)
            {
                var key = $"{m.HomeTeamId}_{m.AwayTeamId}_{m.MatchDate.Date:yyyy-MM-dd}";
                if (!seen.Add(key))
                    toDelete.Add(m);
            }

            if (toDelete.Count > 0)
            {
                _db.Matches.RemoveRange(toDelete);
                await _db.SaveChangesAsync(ct);
            }

            return Ok(new { deleted = toDelete.Count, message = $"Removed {toDelete.Count} duplicate match(es)." });
        }

        // ── Sportmonks — players ──────────────────────────────────────

        [HttpPost("sync-players/sportmonks")]
        public async Task<IActionResult> SyncPlayersSportmonks(CancellationToken ct = default)
        {
            await _fantasySync.SyncPlayersFromSportmonksAsync(ct);
            return Ok(new { message = "Sportmonks player sync triggered. Check logs for progress." });
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
