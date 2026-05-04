using BPFL.API.Data;
using BPFL.API.Features.Fantasy;
using BPFL.API.Features.Matches;
using BPFL.API.Features.Predictions;
using BPFL.API.Shared.External;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BPFL.API.Features.Admin
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

        [HttpPost("matches/dedup")]
        public async Task<IActionResult> DedupMatches(CancellationToken ct = default)
        {
            var all = await _db.Matches.OrderBy(m => m.Id).ToListAsync(ct);

            var seen     = new HashSet<string>();
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
            _ = Task.Run(async () =>
            {
                using var scope = _scopeFactory.CreateScope();
                var svc = scope.ServiceProvider.GetRequiredService<FantasyAutoSyncService>();
                await svc.SyncPlayersFromSportmonksAsync(CancellationToken.None);
            });
            return Ok(new { message = "Player sync started in background. Check Render logs for progress." });
        }

        // ── Debug ────────────────────────────────────────────────────

        [HttpGet("debug/squad/{sportmonksTeamId:int}")]
        public async Task<IActionResult> DebugSquad(int sportmonksTeamId, CancellationToken ct = default)
        {
            using var scope = _scopeFactory.CreateScope();
            var sm = scope.ServiceProvider.GetRequiredService<SportmonksClient>();
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
                ImagePath        = sp.Player?.ImagePath,
            });
            var withPhotos = squad.Count(sp => !string.IsNullOrEmpty(sp.Player?.ImagePath));
            return Ok(new { total = squad.Count, withPhotos, sample = preview });
        }

        [HttpGet("debug/photos")]
        public async Task<IActionResult> DebugPhotos(CancellationToken ct = default)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BPFL_DBContext>();
            var total    = await db.FantasyPlayers.CountAsync(ct);
            var hasPhoto = await db.FantasyPlayers.CountAsync(p => p.PhotoUrl != null, ct);
            var sample   = await db.FantasyPlayers
                .Where(p => p.PhotoUrl != null)
                .Take(5)
                .Select(p => new { p.Id, p.Name, p.PhotoUrl })
                .ToListAsync(ct);
            return Ok(new { total, hasPhoto, noPhoto = total - hasPhoto, sample });
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
