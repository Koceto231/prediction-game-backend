using BPFL.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace BPFL.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MatchController : ControllerBase
    {
        private readonly MatchService matchService;

        public MatchController(MatchService _matchService)
        {
            matchService = _matchService;
        }
        [HttpGet]
        public async Task<IActionResult> GetAllMatches([FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            CancellationToken ct = default)
        {
            var result = await matchService.GetAllAsync(page, pageSize, ct);
            return Ok(result);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetByIdMatch(int id,
            CancellationToken ct = default)
        {
            var result = await matchService.GetByIDMatch(id,ct);
            if (result == null)
            {
                return NotFound();
            }
            return Ok(result);
        }

        [HttpGet("team/{teamId:int}")]
        public async Task<IActionResult> GetMatchesByTeamIdAsync(int teamId,
            CancellationToken ct = default) 
        {
            var result = await matchService.GetMatchesByTeamIdAsync(teamId,ct);
            return Ok(result);
        }

        [HttpGet("upcoming")]
        public async Task<IActionResult> GetFutureMatches([FromQuery] int take = 20,
            CancellationToken ct = default)
        {
            var result = await matchService.GetFutureMatches(take,ct);

            return Ok(result);
        }
    }
}
