using BPFL.API.Models;
using BPFL.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using static BPFL.API.Models.Predictionenums;

namespace BPFL.API.Controllers
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
            [FromQuery] MatchWinner? pick = null,
            [FromQuery] int? scoreHome = null,
            [FromQuery] int? scoreAway = null,
            [FromQuery] bool? btts = null,
            [FromQuery] OverUnderLine? ouLine = null,
            [FromQuery] OverUnderPick? ouPick = null,
            CancellationToken ct = default)
        {
            var result = await _oddsService.GetDynamicOddsAsync(
                matchId, betType, pick, scoreHome, scoreAway, btts, ouLine, ouPick, ct);

            if (result == null)
                return NotFound(new { message = "Odds not available for this match or bet type." });

            return Ok(result);
        }
    }
}
