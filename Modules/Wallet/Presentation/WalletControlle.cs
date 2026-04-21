using BPFL.API.Models;
using BPFL.API.Modules.Wallet.Applications.UseCases;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BPFL.API.Modules.Wallet.Presentation
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class WalletControlle : ControllerBase
    {
        private readonly GetWallet getWallet;
        private readonly ResetDemoBalanceUseCase resetDemoBalanceUseCase;

        public WalletControlle(GetWallet _getWallet, ResetDemoBalanceUseCase _resetDemoBalanceUseCase)
        {
            getWallet = _getWallet;
            resetDemoBalanceUseCase = _resetDemoBalanceUseCase;
        }

        [HttpGet]

        public async Task<IActionResult> GetMyWallet(CancellationToken ct = default)
        {
            var userId = GetUserId();

            if (userId == null)
            {
                return Unauthorized(new { message = "Invalid or missing ID claim" });
            }

            var wallet = await getWallet.ExecuteAsync(userId.Value, ct);

            if (wallet == null)
            {
                return NotFound(new { message = "Wallet not found." });
            }

            return Ok(wallet);
        }

        [HttpPost("reset")]
        public async Task<IActionResult> Reset(CancellationToken ct = default)
        {
            var userId = GetUserId();

            if (userId == null)
            {
                return Unauthorized(new { message = "Invalid or missing ID claim" });
            }

            var balance = await resetDemoBalanceUseCase.ExecuteAsync(userId.Value, ct);

            if (balance == null)
            {
                return NotFound(new { message = "Wallet not found." });
            }


            return Ok(new
            {
                balance
            });


        }


        private int? GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
  
            return int.TryParse(userIdClaim, out var id) ? id : null;
        }
    }
}
