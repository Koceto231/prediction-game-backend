using BPFL.API.Features.Fantasy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BPFL.API.Features.Fantasy
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class FantasyController : ControllerBase
    {
        private readonly FantasyServices _fantasy;
        private readonly FantasyAutoSyncService _autoSync;
        private readonly IConfiguration _config;

        public FantasyController(FantasyServices fantasy, FantasyAutoSyncService autoSync, IConfiguration config)
        {
            _fantasy  = fantasy;
            _autoSync = autoSync;
            _config   = config;
        }

        // ── Gameweek ─────────────────────────────────────────────────

        [HttpGet("gameweek/current")]
        public async Task<IActionResult> GetCurrentGameweek(CancellationToken ct = default)
        {
            var result = await _fantasy.GetCurrentFantasyGameweekAsync(ct);
            if (result == null)
                return NotFound(new { message = "No active fantasy gameweek." });
            return Ok(result);
        }

        // ── Players ──────────────────────────────────────────────────

        [HttpGet("players")]
        public async Task<IActionResult> GetPlayers(CancellationToken ct = default)
        {
            var result = await _fantasy.GetFantasyPlayersAsync(ct);
            return Ok(result);
        }

        // ── Team ─────────────────────────────────────────────────────

        [HttpPost("team")]
        public async Task<IActionResult> CreateTeam([FromBody] CreateFantasyTeamDTO dto, CancellationToken ct = default)
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            await _fantasy.CreateFantasyTeam(userId.Value, dto, ct);
            return Ok(new { message = "Fantasy team created." });
        }

        /// <summary>Get my team for the current (latest non-completed) gameweek.</summary>
        [HttpGet("team")]
        public async Task<IActionResult> GetMyTeamCurrent(CancellationToken ct = default)
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var result = await _fantasy.GetMyTeamForCurrentGameweekAsync(userId.Value, ct);
            if (result == null)
                return Ok(new { hasTeam = false });

            return Ok(result);
        }

        /// <summary>
        /// GET /Fantasy/team/squad
        /// Returns the user's most recent squad selection (players + captainId)
        /// regardless of which GW it was for. Used by the draft page for carry-over.
        /// </summary>
        [HttpGet("team/squad")]
        public async Task<IActionResult> GetLatestSquad(CancellationToken ct = default)
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var (players, captainId) = await _fantasy.GetLatestSquadAsync(userId.Value, ct);
            return Ok(new { players, captainId });
        }

        /// <summary>Get my team for a specific gameweek.</summary>
        [HttpGet("team/{gameweekId:int}")]
        public async Task<IActionResult> GetMyTeam(int gameweekId, CancellationToken ct = default)
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var result = await _fantasy.GetMyFantasyTeamAsync(userId.Value, gameweekId, ct);
            if (result == null)
                return Ok(new { hasTeam = false });

            return Ok(result);
        }

        // ── Selection ─────────────────────────────────────────────────

        [HttpPost("selection")]
        public async Task<IActionResult> SaveSelection([FromBody] SaveFantasySelectionDTO dto, CancellationToken ct = default)
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            try
            {
                await _fantasy.SaveFantasySelectionAsync(userId.Value, dto, ct);
                return Ok(new { message = "Selection saved." });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        // ── Leaderboard ───────────────────────────────────────────────

        [HttpGet("leaderboard/{gameweekId:int}")]
        public async Task<IActionResult> GetLeaderboard(int gameweekId, CancellationToken ct = default)
        {
            var result = await _fantasy.GetFantasyLeaderboardAsync(gameweekId, ct);
            return Ok(result);
        }

        [HttpGet("leaderboard/current")]
        public async Task<IActionResult> GetCurrentLeaderboard(CancellationToken ct = default)
        {
            var gameweek = await _fantasy.GetCurrentFantasyGameweekAsync(ct);
            var result = await _fantasy.GetFantasyLeaderboardAsync(gameweek.Id, ct);
            return Ok(new { gameweek, leaderboard = result });
        }

        // ── Admin endpoints ───────────────────────────────────────────

        [HttpPost("admin/gameweek")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateGameweek([FromBody] CreateFantasyGameweekDTO dto, CancellationToken ct = default)
        {
            var result = await _fantasy.CreateGameweekAsync(dto, ct);
            return Ok(result);
        }

        [HttpPost("admin/players")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AddPlayer([FromBody] AddFantasyPlayerDTO dto, CancellationToken ct = default)
        {
            var result = await _fantasy.AddPlayerAsync(dto, ct);
            return Ok(result);
        }

        [HttpPost("admin/stats")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> SubmitStats([FromBody] SubmitPlayerStatsDTO dto, CancellationToken ct = default)
        {
            await _fantasy.SubmitPlayerStatsAsync(dto, ct);
            return Ok(new { message = "Stats submitted and points calculated." });
        }

        [HttpPost("admin/score/{gameweekId:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CalculateScores(int gameweekId, CancellationToken ct = default)
        {
            await _fantasy.CalculateGameweekScoresAsync(gameweekId, ct);
            return Ok(new { message = $"Scores calculated for gameweek {gameweekId}." });
        }

        /// <summary>
        /// POST /Fantasy/admin/gameweek/advance
        /// Creates the next gameweek automatically with Friday 12:00 → Tuesday 05:00 window.
        /// </summary>
        [HttpPost("admin/gameweek/advance")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AdvanceGameweek(CancellationToken ct = default)
        {
            var gw = await _autoSync.AdvanceGameweekAsync(ct);
            return gw == null
                ? Ok(new { message = "No upcoming matchday found to create a gameweek for." })
                : Ok(new { message = $"Gameweek {gw} created (Fri 12:00 → Tue 05:00 UTC)." });
        }

        /// <summary>
        /// POST /Fantasy/admin/gameweek/force?anchorDate=2026-05-10
        /// Creates the next gameweek anchored to the given date (or today if omitted),
        /// regardless of whether matches exist in the DB.
        /// </summary>
        /// <summary>GET /Fantasy/admin/gameweeks — list all gameweeks (for admin UI)</summary>
        [HttpGet("admin/gameweeks")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAllGameweeks(CancellationToken ct = default)
        {
            var result = await _fantasy.GetAllGameweeksAsync(ct);
            return Ok(result);
        }

        /// <summary>POST /Fantasy/admin/gameweek/{id}/complete — mark GW as completed</summary>
        [HttpPost("admin/gameweek/{id:int}/complete")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CompleteGameweek(int id, CancellationToken ct = default)
        {
            try
            {
                await _fantasy.CompleteGameweekAsync(id, ct);
                return Ok(new { message = $"Gameweek {id} marked as completed." });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        [HttpPost("admin/gameweek/force")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ForceCreateGameweek(
            [FromQuery] DateTime? anchorDate = null,
            [FromQuery] int? gwNumber = null,
            [FromQuery] DateTime? deadline = null,
            CancellationToken ct = default)
        {
            var anchor = anchorDate?.ToUniversalTime() ?? DateTime.UtcNow;
            var gw = await _autoSync.ForceCreateGameweekAsync(anchor, gwNumber, deadline?.ToUniversalTime(), ct);
            return Ok(new { message = $"Gameweek {gw} created." });
        }

        [HttpPost("admin/recalc-prices")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> RecalcPrices(CancellationToken ct = default)
        {
            var updated = await _fantasy.RecalcPlayerPricesAsync(ct);
            return Ok(new { message = $"Prices recalculated for {updated} players." });
        }

        /// <summary>Sync fantasy players from team squads via football-data.org API.</summary>
        [HttpPost("admin/sync-players")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> SyncPlayers(CancellationToken ct = default)
        {
            var codes = _config.GetSection("BackgroundJobs:LeagueCodes").Get<string[]>()
                        ?? new[] { "PL" };
            await _autoSync.SyncPlayersFromSquadsAsync(codes, ct);
            return Ok(new { message = "Player sync complete." });
        }

        /// <summary>Auto-create fantasy gameweeks from matchdays.</summary>
        [HttpPost("admin/sync-gameweeks")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> SyncGameweeks(CancellationToken ct = default)
        {
            await _autoSync.SyncGameweeksFromMatchdaysAsync(ct);
            return Ok(new { message = "Gameweek sync complete." });
        }

        // ── Helpers ───────────────────────────────────────────────────
        private int? GetUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(claim, out var id) ? id : null;
        }
    }
}
