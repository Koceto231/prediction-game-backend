using BPFL.API.Models.DTO;
using BPFL.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BPFL.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class BetController : ControllerBase
    {
        private readonly BetService _betService;

        public BetController(BetService betService)
        {
            _betService = betService;
        }

        [HttpPost]
        public async Task<IActionResult> PlaceBet([FromBody] PlaceBetDTO dto, CancellationToken ct)
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            try
            {
                var result = await _betService.PlaceBetAsync(userId.Value, dto, ct);
                return Ok(result);
            }
            catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
            catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        }

        [HttpGet("me")]
        public async Task<IActionResult> GetMyBets(CancellationToken ct)
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var bets = await _betService.GetMyBetsAsync(userId.Value, ct);
            return Ok(bets);
        }

        private int? GetUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(claim, out var id) ? id : null;
        }
    }
}
