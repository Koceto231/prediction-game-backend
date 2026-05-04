using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BPFL.API.Features.Profile
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ProfileController : ControllerBase
    {
        private readonly ProfileService profileService;

        public ProfileController(ProfileService _profileService)
        {
            profileService = _profileService;
        }

        [HttpGet("me")]
        public async Task<IActionResult> GetMe(CancellationToken ct = default)
        {
            var userId = GetUserId();
            if (userId == null)
                return Unauthorized(new { message = "Invalid user token." });

            try
            {
                var user = await profileService.GetCurrentProfileAsync(userId.Value, ct);
                return Ok(user);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        [HttpGet("stats")]
        public async Task<IActionResult> GetStatsAsync(CancellationToken ct = default)
        {
            var userId = GetUserId();
            if (userId == null)
                return Unauthorized(new { message = "Invalid user token." });

            var stats = await profileService.GetStatsAsync(userId.Value, ct);
            return Ok(stats);
        }

        private int? GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdClaim, out var id) ? id : null;
        }
    }
}
