using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BPFL.API.Features.Matches
{
    [ApiController]
    [Route("api/[controller]")]
    public class MatchController : ControllerBase
    {
        private readonly MatchService _matchService;
        private readonly OddsService  _oddsService;

        public MatchController(MatchService matchService, OddsService oddsService)
        {
            _matchService = matchService;
            _oddsService  = oddsService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllMatches(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            CancellationToken ct = default)
        {
            var result = await _matchService.GetAllAsync(page, pageSize, ct);
            return Ok(result);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetByIdMatch(int id, CancellationToken ct = default)
        {
            var result = await _matchService.GetByIDMatch(id, ct);
            if (result == null) return NotFound();
            return Ok(result);
        }

        [HttpGet("team/{teamId:int}")]
        public async Task<IActionResult> GetMatchesByTeamIdAsync(int teamId, CancellationToken ct = default)
        {
            var result = await _matchService.GetMatchesByTeamIdAsync(teamId, ct);
            return Ok(result);
        }

        [HttpGet("upcoming")]
        public async Task<IActionResult> GetFutureMatches([FromQuery] int take = 20, CancellationToken ct = default)
        {
            var result = await _matchService.GetFutureMatches(take, ct);
            return Ok(result);
        }

        /// <summary>
        /// Returns all active players for both teams in a match with goalscorer odds.
        /// Used by the Goalscorer betting panel.
        /// </summary>
        [HttpGet("{matchId:int}/players")]
        [Authorize]
        public async Task<IActionResult> GetMatchPlayers(int matchId, CancellationToken ct = default)
        {
            var players = await _oddsService.GetMatchPlayersWithOddsAsync(matchId, ct);
            if (players.Count == 0)
                return NotFound(new { message = "No players found for this match. Make sure players are synced." });
            return Ok(players);
        }
    }
}
