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
        public async Task<IActionResult> GetAllMatches()
        {
            var result = await matchService.GetAllAsync();
            return Ok(result);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetByIdMatch(int id)
        {
            var result = await matchService.GetByIDMatch(id);
            if (result == null)
            {
                return NotFound();
            }
            return Ok(result);
        }

        [HttpGet("team/{teamId:int}")]
        public async Task<IActionResult> GetMatchesByTeamIdAsync(int teamId) 
        {
            var result = await matchService.GetMatchesByTeamIdAsync(teamId);
            return Ok(result);
        }

        [HttpGet("upcoming")]
        public async Task<IActionResult> GetFutureMatches([FromQuery] int take)
        {
            var result = await matchService.GetFutureMatches(take);

            return Ok(result);
        }
    }
}
