using BPFL.API.Models.DTO;
using BPFL.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BPFL.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AuthServices authServices;
        private readonly GoogleAuthService googleAuthService;

        public AuthController(AuthServices _authServices, GoogleAuthService _googleAuthService)
        {
            authServices = _authServices;
            googleAuthService = _googleAuthService;
        }

        [EnableRateLimiting("auth")]
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDTO registerDTO, CancellationToken ct = default)
        {
            var result = await authServices.RegisterAsync(registerDTO, ct);

            if (!result.Success)
            {
                return BadRequest(new { message = result.Error });
            }

            return Ok(result.User);
        }

        [EnableRateLimiting("auth")]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDTO loginDto, CancellationToken ct = default)
        {
            var result = await authServices.LoginAsync(loginDto, ct);

            if (!result.Success)
            {
                return Unauthorized(new { message = result.Error });
            }

            return Ok(result.Tokens);
        }

        [EnableRateLimiting("auth")]
        [HttpPost("google")]
        public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginDTO googleLoginDTO, CancellationToken ct = default)
        {
            if (googleLoginDTO == null || string.IsNullOrWhiteSpace(googleLoginDTO.IdToken))
            {
                return BadRequest(new { message = "ID token is required." });
            }

            var result = await googleAuthService.LoginWithGoogleAsync(googleLoginDTO.IdToken, ct);

            if (!result.Success)
            {
                return Unauthorized(new { message = result.Error });
            }

            return Ok(result.Tokens);
        }

        
        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequestDTO dto, CancellationToken ct = default)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.RefreshToken))
            {
                return BadRequest(new { message = "Refresh token is required." });
            }

            var result = await authServices.RefreshTokenAsync(dto.RefreshToken, ct);

            if (!result.Success)
            {
                return Unauthorized(new { message = result.Error });
            }

            return Ok(result.Tokens);
        }

        [HttpGet("verify-email")]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> VerifyEmail([FromQuery] string token, CancellationToken ct = default)
        {
            var result = await authServices.VerifyEmailAsync(token, ct);

            if (!result)
                return BadRequest(new { message = "Invalid or expired token." });

            return Ok(new { message = "Email verified successfully." });
        }

        [EnableRateLimiting("auth")]
        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPaswwordDTO dto, CancellationToken ct = default)
        {
            var result = await authServices.ForgotPasswordAsync(dto, ct);

            if (!result.Success)
                return BadRequest(new { message = result.Error });

            return Ok(new { message = "If an account with that email exists, a reset link has been sent." });
        }

        [EnableRateLimiting("auth")]
        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDTO dto, CancellationToken ct = default)
        {
            var result = await authServices.ResetPasswordAsync(dto, ct);

            if (!result.Success)
                return BadRequest(new { message = result.Error });

            return Ok(new { message = "Password reset successfully." });
        }

        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout([FromBody] RefreshTokenRequestDTO dto, CancellationToken ct = default)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.RefreshToken))
            {
                return BadRequest(new { message = "Refresh token is required." });
            }

            var result = await authServices.RevokeRefreshTokenAsync(dto.RefreshToken, ct);

            if (!result.Success)
            {
                return Unauthorized(new { message = result.Error });
            }

            return Ok(new { message = "Logged out successfully." });
        }
    }
}


