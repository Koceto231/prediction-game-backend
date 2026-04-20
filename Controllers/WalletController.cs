using BPFL.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BPFL.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class WalletController : ControllerBase
    {
        private readonly WalletService _walletService;

        public WalletController(WalletService walletService)
        {
            _walletService = walletService;
        }

        [HttpGet]
        public async Task<IActionResult> GetBalance(CancellationToken ct)
        {
            var userId = GetUserId();
            if (userId == null)
                return Unauthorized();

            var balance = await _walletService.GetBalanceAsync(userId.Value, ct);
            return Ok(new { balance });
        }

        [HttpPost("topup")]
        public async Task<IActionResult> TopUp(CancellationToken ct)
        {
            var userId = GetUserId();
            if (userId == null)
                return Unauthorized();

            try
            {
                var balance = await _walletService.TopUpAsync(userId.Value, ct);
                return Ok(new { balance });
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
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(claim, out var id) ? id : null;
        }
    }
}
