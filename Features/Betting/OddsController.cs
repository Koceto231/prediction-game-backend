using BPFL.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using static BPFL.API.Models.Predictionenums;

namespace BPFL.API.Features.Betting
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class OddsController : ControllerBase
    {
        private readonly OddsService _oddsService;

        public OddsController(OddsService oddsService)
        {
            _oddsService = oddsService;
        }

        [HttpGet("{matchId:int}")]
        public async Task<IActionResult> GetOdds(
            int matchId,
            [FromQuery] BetType betType,
            // 1X2
            [FromQuery] MatchWinner? pick         = null,
            // Exact score
            [FromQuery] int? scoreHome            = null,
            [FromQuery] int? scoreAway            = null,
            // BTTS
            [FromQuery] bool? btts                = null,
            // O/U goals
            [FromQuery] OverUnderLine? ouLine     = null,
            [FromQuery] OverUnderPick? ouPick     = null,
            // Goalscorer
            [FromQuery] int? goalscorerId         = null,
            // Corners / Yellow Cards — numeric line (e.g. 9.5) + ouPick
            [FromQuery] decimal? lineValue        = null,
            // Double Chance
            [FromQuery] DoubleChancePick? dcPick  = null,
            CancellationToken ct = default)
        {
            var result = await _oddsService.GetDynamicOddsAsync(
                matchId, betType,
                pick, scoreHome, scoreAway, btts, ouLine, ouPick,
                goalscorerId, lineValue, dcPick, ct);

            if (result == null)
                return NotFound(new { message = "Odds not available for this match or bet type." });

            return Ok(result);
        }
    }
}
