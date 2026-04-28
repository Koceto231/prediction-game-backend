using BPFL.API.Data;
using BPFL.API.Services;
using BPFL.API.Services.FantasyServices;
using BPFL.API.Services.MatchServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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
        private readonly IServiceScopeFactory _scopeFactory;

        public AdminSyncController(
            PredictionScoringService scoring,
            SportmonksMatchSyncService sportmonks,
            FantasyAutoSyncService fantasySync,
            BPFL_DBContext db,
            IServiceScopeFactory scopeFactory)
        {
            _scoring      = scoring;
            _sportmonks   = sportmonks;
            _fantasySync  = fantasySync;
            _db           = db;
            _scopeFactory = scopeFactory;
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

        // ── Historical match import ───────────────────────────────────

        /// <summary>
        /// Import finished matches going back daysBack days via Sportmonks fixtures/between endpoint.
        /// </summary>
        [HttpPost("matches/history")]
        public async Task<IActionResult> ImportHistory(
            [FromQuery] string leagueCode = "BGL",
            [FromQuery] int daysBack = 365,
            CancellationToken ct = default)
        {
            try
            {
                var (added, updated) = await _sportmonks.SyncLeagueHistoryAsync(leagueCode, daysBack, ct);
                return Ok(new { added, updated, message = $"History sync done for {leagueCode} ({daysBack} days back)." });
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
        public IActionResult SyncPlayersSportmonks()
        {
            // Fire-and-forget — sync can take several minutes for many teams
            _ = Task.Run(async () =>
            {
                using var scope = _scopeFactory.CreateScope();
                var svc = scope.ServiceProvider.GetRequiredService<FantasyAutoSyncService>();
                await svc.SyncPlayersFromSportmonksAsync(CancellationToken.None);
            });
            return Ok(new { message = "Player sync started in background. Check Render logs for progress." });
        }

        // ── Debug ────────────────────────────────────────────────────

        /// <summary>Returns raw Sportmonks squad for a team so we can inspect position fields.</summary>
        [HttpGet("debug/squad/{sportmonksTeamId:int}")]
        public async Task<IActionResult> DebugSquad(int sportmonksTeamId, CancellationToken ct = default)
        {
            using var scope = _scopeFactory.CreateScope();
            var sm = scope.ServiceProvider.GetRequiredService<BPFL.API.Services.External.SportmonksClient>();
            var squad = await sm.GetSquadByTeamIdAsync(sportmonksTeamId, ct);
            var preview = squad.Take(5).Select(sp => new
            {
                sp.PlayerId,
                sp.PositionId,
                PlayerName       = sp.Player?.Name,
                PlayerCommonName = sp.Player?.CommonName,
                PlayerPosId      = sp.Player?.PositionId,
                PosName          = sp.Player?.Position?.Name,
                PosCode          = sp.Player?.Position?.Code,
            });
            return Ok(new { total = squad.Count, sample = preview });
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
