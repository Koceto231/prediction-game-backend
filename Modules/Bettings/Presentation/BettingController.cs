using BPFL.API.Modules.Bettings.Application.DTOs;
using BPFL.API.Modules.Bettings.Application.UseCases;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BPFL.API.Modules.Bettings.Presentation
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class BettingController : ControllerBase
    {
       private readonly PlaceBetUseCase placeBetUseCase;

        public BettingController(PlaceBetUseCase _placeBetUseCase)
        {
            placeBetUseCase = _placeBetUseCase;
        }

        [HttpPost("place")]
        public async Task<IActionResult> PlaceBet([FromBody] PlaceBetRequestDTO placeBetRequestDTO, CancellationToken ct = default)
        {
            var userId = GetUserId();

            if (userId == null)
            {
                return Unauthorized(new { message = "Invalid or missing ID claim" });
            }

            try
            {
                var result = placeBetUseCase.ExecuteAsync(userId.Value, placeBetRequestDTO, ct);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
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

        private int? GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            return int.TryParse(userIdClaim, out var id) ? id : null;
        }
    }
}
