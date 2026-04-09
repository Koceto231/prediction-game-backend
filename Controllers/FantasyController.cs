using BPFL.API.Models.FantasyDTO;
using BPFL.API.Services.FantasyServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BPFL.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class FantasyController : ControllerBase
    {
        private readonly FantasyServices fantasyServices;

        public FantasyController(FantasyServices _fantasyServices)
        {
            fantasyServices = _fantasyServices;
        }

        [HttpGet("gameweek/current")]

        public async Task<IActionResult> GetCurrentFantasyGameweek(CancellationToken ct = default)
        {
            var result = await fantasyServices.GetCurrentFantasyGameweekAsync(ct);
            return Ok(result);
        }


        [HttpGet("players")]
        public async Task<IActionResult> GetPlayers(CancellationToken ct = default)
        {
            var result = await fantasyServices.GetFantasyPlayersAsync(ct);
            return Ok(result);
        }

        [HttpPost("team")]

        public async Task<IActionResult> CreateFantasyTeam([FromBody] CreateFantasyTeamDTO createFantasyTeamDTO,CancellationToken ct = default)
        {
            var userId = GetUserId();
            if (userId == null)
                return Unauthorized(new { message = "Invalid user token." });

            await fantasyServices.CreateFantasyTeam(userId.Value,createFantasyTeamDTO,ct);

            return Ok();

           
        }

        [HttpPost("selection")]
        public async Task<IActionResult> PostSelectionPlayers([FromBody] SaveFantasySelectionDTO saveFantasySelectionDTO,CancellationToken ct = default)
        {
            var userId = GetUserId();
            if (userId == null)
                return Unauthorized(new { message = "Invalid user token." });

             await fantasyServices.SaveFantasySelectionAsync(userId.Value, saveFantasySelectionDTO, ct);

            return Ok();
        }

        [HttpGet("team/{fantasyGameweekId}")]

        public async Task<IActionResult> GetMyTeam(int fantasyGameweekId,CancellationToken ct = default)
        {
            var userId = GetUserId();
            if (userId == null)
                return Unauthorized(new { message = "Invalid user token." });

            var result = await fantasyServices.GetMyFantasyTeamAsync(userId.Value, fantasyGameweekId,ct);
            return Ok(result);

        }

        private int? GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            return int.TryParse(userIdClaim, out var id) ? id : null;
        }
    }
}
