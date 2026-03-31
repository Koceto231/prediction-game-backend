using BPFL.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BPFL.API.Controllers
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

            var user = await profileService.GetCurrentProfileAsync(userId, ct);

            return Ok(user);
        }

        [HttpGet("stats")]
        public async Task<IActionResult> GetStatsAsync(CancellationToken ct = default)
        {
            var userId = GetUserId();

            var stats = await profileService.GetStatsAsync(userId, ct);

            return Ok(stats);
        }

        private int GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);

            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
                throw new UnauthorizedAccessException("Invalid user token.");

            return userId;
        }
    }
}