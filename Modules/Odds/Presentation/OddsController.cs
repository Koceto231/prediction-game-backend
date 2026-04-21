using BPFL.API.Modules.Odds.Application.UseCases;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BPFL.API.Modules.Odds.Presentation
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class OddsController : ControllerBase
    {
        private readonly GetMatchMarketsUseCase getMatchMarketsUseCase;

        public OddsController(GetMatchMarketsUseCase getMatchMarketsUseCase)
        {
            this.getMatchMarketsUseCase = getMatchMarketsUseCase;
        }

        [HttpGet("match/{matchId:int}")]
        public async Task<IActionResult> GetMatchMarkets(int matchId, CancellationToken ct = default)
        {
            var result = await getMatchMarketsUseCase.ExecuteAsync(matchId, ct);
            return Ok(result);
        }
    }
    }
