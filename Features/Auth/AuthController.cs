using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BPFL.API.Features.Auth
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AuthServices _auth;
        private readonly GoogleAuthService _google;
        private readonly IWebHostEnvironment _env;
        private readonly IConfiguration _config;

        public AuthController(
            AuthServices auth,
            GoogleAuthService google,
            IWebHostEnvironment env,
            IConfiguration config)
        {
            _auth   = auth;
            _google = google;
            _env    = env;
            _config = config;
        }

        // ── Register ──────────────────────────────────────────────────

        [EnableRateLimiting("auth")]
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDTO dto, CancellationToken ct = default)
        {
            var result = await _auth.RegisterAsync(dto, ct);
            if (!result.Success)
                return BadRequest(new { message = result.Error });

            return Ok(result.User);
        }

        // ── Login ─────────────────────────────────────────────────────

        [EnableRateLimiting("auth")]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDTO dto, CancellationToken ct = default)
        {
            var result = await _auth.LoginAsync(dto, ct);
            if (!result.Success)
                return Unauthorized(new { message = result.Error });

            SetTokenCookies(result.Tokens!.AccessToken, result.Tokens.RefreshToken);
            return Ok(result.User);   // only user info — tokens are in HttpOnly cookies
        }

        // ── Google login ──────────────────────────────────────────────

        [EnableRateLimiting("auth")]
        [HttpPost("google")]
        public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginDTO dto, CancellationToken ct = default)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.IdToken))
                return BadRequest(new { message = "ID token is required." });

            var result = await _google.LoginWithGoogleAsync(dto.IdToken, ct);
            if (!result.Success)
                return Unauthorized(new { message = result.Error });

            SetTokenCookies(result.Tokens!.AccessToken, result.Tokens.RefreshToken);
            return Ok(result.User);
        }

        // ── Refresh ───────────────────────────────────────────────────

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh(CancellationToken ct = default)
        {
            var rawRefresh = Request.Cookies["refresh_token"];
            if (string.IsNullOrWhiteSpace(rawRefresh))
                return Unauthorized(new { message = "No refresh token." });

            var result = await _auth.RefreshTokenAsync(rawRefresh, ct);
            if (!result.Success)
            {
                ClearTokenCookies();
                return Unauthorized(new { message = result.Error });
            }

            SetTokenCookies(result.Tokens!.AccessToken, result.Tokens.RefreshToken);
            return Ok(new { message = "Token refreshed." });
        }

        // ── Logout ────────────────────────────────────────────────────

        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout(CancellationToken ct = default)
        {
            var rawRefresh = Request.Cookies["refresh_token"];
            if (!string.IsNullOrWhiteSpace(rawRefresh))
                await _auth.RevokeRefreshTokenAsync(rawRefresh, ct);

            ClearTokenCookies();
            return Ok(new { message = "Logged out successfully." });
        }

        // ── Email / Password ──────────────────────────────────────────

        [HttpGet("verify-email")]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> VerifyEmail([FromQuery] string token, CancellationToken ct = default)
        {
            var result = await _auth.VerifyEmailAsync(token, ct);
            if (!result)
                return BadRequest(new { message = "Invalid or expired token." });

            return Ok(new { message = "Email verified successfully." });
        }

        [EnableRateLimiting("auth")]
        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPaswwordDTO dto, CancellationToken ct = default)
        {
            var result = await _auth.ForgotPasswordAsync(dto, ct);
            if (!result.Success)
                return BadRequest(new { message = result.Error });

            return Ok(new { message = "If an account with that email exists, a reset link has been sent." });
        }

        [EnableRateLimiting("auth")]
        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDTO dto, CancellationToken ct = default)
        {
            var result = await _auth.ResetPasswordAsync(dto, ct);
            if (!result.Success)
                return BadRequest(new { message = result.Error });

            return Ok(new { message = "Password reset successfully." });
        }

        // ── Cookie helpers ────────────────────────────────────────────

        private void SetTokenCookies(string accessToken, string refreshToken)
        {
            var isDev        = _env.IsDevelopment();
            var expiryMins   = int.Parse(_config["Jwt:ExpirationMinutes"] ?? "15");

            var baseOpts = new CookieOptions
            {
                HttpOnly = true,
                Secure   = !isDev,                                        // HTTPS only in production
                SameSite = isDev ? SameSiteMode.Lax : SameSiteMode.None, // None required for cross-origin
                Path     = "/"
            };

            // Access token — sent with every request
            Response.Cookies.Append("access_token", accessToken, new CookieOptions
            {
                HttpOnly = baseOpts.HttpOnly,
                Secure   = baseOpts.Secure,
                SameSite = baseOpts.SameSite,
                Path     = "/",
                Expires  = DateTime.UtcNow.AddMinutes(expiryMins)
            });

            // Refresh token — only sent to /api/Auth endpoints
            Response.Cookies.Append("refresh_token", refreshToken, new CookieOptions
            {
                HttpOnly = baseOpts.HttpOnly,
                Secure   = baseOpts.Secure,
                SameSite = baseOpts.SameSite,
                Path     = "/api/Auth",
                Expires  = DateTime.UtcNow.AddDays(7)
            });
        }

        private void ClearTokenCookies()
        {
            var isDev = _env.IsDevelopment();
            var opts  = new CookieOptions
            {
                Secure   = !isDev,
                SameSite = isDev ? SameSiteMode.Lax : SameSiteMode.None
            };

            Response.Cookies.Delete("access_token",  new CookieOptions { Path = "/",         Secure = opts.Secure, SameSite = opts.SameSite });
            Response.Cookies.Delete("refresh_token", new CookieOptions { Path = "/api/Auth", Secure = opts.Secure, SameSite = opts.SameSite });
        }
    }
}
