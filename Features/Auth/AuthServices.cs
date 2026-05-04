using BCrypt.Net;
using BPFL.API.Data;
using BPFL.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;



namespace BPFL.API.Features.Auth
{

    public class AuthResult
    {
        public bool Success { get; private set; }
        public string? Error { get; private set; }
        public UserResponseDTO? User { get; private set; }
        public AuthTokenDTO? Tokens { get; private set; }

        public static AuthResult Ok() => new() { Success = true };
        public static AuthResult Ok(AuthTokenDTO tokens) => new() { Success = true, Tokens = tokens };
        public static AuthResult Ok(UserResponseDTO user) => new() { Success = true, User = user };
        public static AuthResult Ok(AuthTokenDTO tokens, UserResponseDTO user) => new() { Success = true, Tokens = tokens, User = user };
        public static AuthResult Fail(string error) => new() { Success = false, Error = error };
    }

    public class AuthServices
    {
        private readonly BPFL_DBContext bPFL_DBContext;
        private readonly ILogger<AuthServices> _logger;
        private readonly IConfiguration configuration;
        private readonly EmailService emailService;

        // FIX: Cache JWT config at construction time instead of re-reading on every token generation
        private readonly string _jwtKey;
        private readonly string _jwtIssuer;
        private readonly string _jwtAudience;
        private readonly int _jwtExpirationMinutes;
        private readonly SymmetricSecurityKey _jwtSecurityKey;

        // FIX: Compile the special chars set once instead of string.Contains() per char on every validation
        private static readonly HashSet<char> _specialChars =
            new("!@#$%^&*()_+-=[]{}|;:,.<>?");

        public AuthServices(BPFL_DBContext _bPFLDBContext, IConfiguration _configuration, ILogger<AuthServices> logger, EmailService _emailService)
        {
            bPFL_DBContext = _bPFLDBContext;
            configuration = _configuration;
            _logger = logger;
            emailService = _emailService;

            _jwtKey = configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key is not configured.");
            _jwtIssuer = configuration["Jwt:Issuer"]!;
            _jwtAudience = configuration["Jwt:Audience"]!;
            _jwtExpirationMinutes = int.Parse(configuration["Jwt:ExpirationMinutes"] ?? "15");
            _jwtSecurityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtKey));
        }

        public async Task<AuthResult> RegisterAsync(RegisterDTO registerDTO, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(registerDTO);

            var validationError = ValidatePassword(registerDTO.Password);
            if (validationError != null) return AuthResult.Fail(validationError);

            var emailValidator = new EmailAddressAttribute();
            if (string.IsNullOrWhiteSpace(registerDTO.Email) || !emailValidator.IsValid(registerDTO.Email))
                return AuthResult.Fail("Invalid email format.");

            if (string.IsNullOrWhiteSpace(registerDTO.Username) || registerDTO.Username.Length < 3)
                return AuthResult.Fail("Invalid username.");

            var normalizedEmail = NormalizeEmail(registerDTO.Email);

            if (await bPFL_DBContext.Users.AnyAsync(u => u.Email == normalizedEmail, ct))
                return AuthResult.Fail("Email already exists.");

            string passwordHash = BCrypt.Net.BCrypt.EnhancedHashPassword(registerDTO.Password, 13);

            var emailToken = GenerateEmailToken();

            var user = new User
            {
                Email = normalizedEmail,
                Username = registerDTO.Username.Trim(),
                Password = passwordHash,
                Role = "User",
                EmailVerificationToken = emailToken,
                EmailVerificationTokenExpires = DateTime.UtcNow.AddMinutes(60),
                IsEmailVerified = false
            };

            bPFL_DBContext.Users.Add(user);
            await bPFL_DBContext.SaveChangesAsync(ct);

            var frontendBaseUrl = configuration["App:FrontendBaseUrl"];
            var verifyUrl = $"{frontendBaseUrl}/verify-email?token={Uri.EscapeDataString(emailToken)}";

            await emailService.SendVerificationEmailAsync(user.Email, verifyUrl, ct);

            return AuthResult.Ok(new UserResponseDTO
            {
                Id = user.Id,
                Email = user.Email,
                Username = user.Username,
                Role = user.Role
            });
        }

        public async Task<AuthResult> LoginAsync(LoginDTO loginDTO, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(loginDTO);

            if (string.IsNullOrWhiteSpace(loginDTO.Email) || string.IsNullOrWhiteSpace(loginDTO.Password))
                return AuthResult.Fail("Invalid credentials.");

            var normalizedEmail = NormalizeEmail(loginDTO.Email);


            var user = await bPFL_DBContext.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Email == normalizedEmail, ct);

            if (user == null)
            {
                _logger.LogWarning("Failed login attempt for email: {Email}", normalizedEmail);
                return AuthResult.Fail("Invalid credentials.");
            }

            if (!user.IsEmailVerified)
                return AuthResult.Fail("Please verify your email first.");

            if (user.Password == null)
            {
                _logger.LogWarning("User {Email} has no password (Google-only account)", normalizedEmail);
                return AuthResult.Fail("This account uses Google login. Please sign in with Google.");
            }

            var isValidPassword = BCrypt.Net.BCrypt.EnhancedVerify(loginDTO.Password, user.Password);

            if (!isValidPassword)
            {
                _logger.LogWarning("Failed login attempt for email: {Email}", normalizedEmail);
                return AuthResult.Fail("Invalid credentials.");
            }

            var accesstoken = GenerateJwtToken(user);
            var refreshToken = await CreateAndStoreRefreshTokenAsync(user, ct);

            _logger.LogInformation("User logged in: {UserId}", user.Id);

            return AuthResult.Ok(
                new AuthTokenDTO { AccessToken = accesstoken, RefreshToken = refreshToken },
                new UserResponseDTO { Id = user.Id, Email = user.Email, Username = user.Username, Role = user.Role }
            );
        }

        public async Task<AuthResult> RefreshTokenAsync(string rawRefreshToken, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(rawRefreshToken))
                return AuthResult.Fail("Invalid refresh token.");

            var hashedToken = HashToken(rawRefreshToken);

            var storedToken = await bPFL_DBContext.RefreshTokens
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.TokenHash == hashedToken, ct);

            if (storedToken == null || !storedToken.IsActive)
                return AuthResult.Fail("Invalid refresh token.");

            storedToken.RevokedAt = DateTime.UtcNow;
            await bPFL_DBContext.SaveChangesAsync(ct);

            var newAccessToken = GenerateJwtToken(storedToken.User);
            var newRefreshToken = await CreateAndStoreRefreshTokenAsync(storedToken.User, ct);

            return AuthResult.Ok(new AuthTokenDTO
            {
                AccessToken = newAccessToken,
                RefreshToken = newRefreshToken
            });
        }

        public async Task<AuthResult> RevokeRefreshTokenAsync(string rawRefreshToken, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(rawRefreshToken))
                return AuthResult.Fail("Invalid refresh token.");

            var hashedToken = HashToken(rawRefreshToken);

            var storedToken = await bPFL_DBContext.RefreshTokens
                .FirstOrDefaultAsync(rt => rt.TokenHash == hashedToken, ct);

            if (storedToken == null || !storedToken.IsActive)
                return AuthResult.Fail("Invalid refresh token.");

            storedToken.RevokedAt = DateTime.UtcNow;
            await bPFL_DBContext.SaveChangesAsync(ct);

            return AuthResult.Ok();
        }

        public async Task<bool> VerifyEmailAsync(string emailToken, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(emailToken))
                return false;

            var user = await bPFL_DBContext.Users
                .FirstOrDefaultAsync(c => c.EmailVerificationToken == emailToken, ct);

            if (user == null)
                return false;

            if (user.EmailVerificationTokenExpires == null || user.EmailVerificationTokenExpires < DateTime.UtcNow)
                return false;

            user.IsEmailVerified = true;
            user.EmailVerificationToken = null;
            user.EmailVerificationTokenExpires = null;

            await bPFL_DBContext.SaveChangesAsync(ct);
            return true;
        }

        public async Task<AuthResult> ForgotPasswordAsync(ForgotPaswwordDTO forgotPaswwordDTO, CancellationToken ct = default)
        {
            if (forgotPaswwordDTO == null || string.IsNullOrWhiteSpace(forgotPaswwordDTO.Email))
                return AuthResult.Fail("Email is required.");

            var normilizeEmail = NormalizeEmail(forgotPaswwordDTO.Email);

            var user = await bPFL_DBContext.Users.FirstOrDefaultAsync(u => u.Email == normilizeEmail, ct);


            if (user == null)
                return AuthResult.Ok();

            var resetToken = GenerateEmailToken();

            user.PasswordResetToken = resetToken;
            user.PasswordResetTokenExpires = DateTime.UtcNow.AddMinutes(30);

            await bPFL_DBContext.SaveChangesAsync(ct);

            var frontendBaseUrl = configuration["App:FrontendBaseUrl"] ?? "https://localhost:5173";
            var resetUrl = $"{frontendBaseUrl}/reset-password?token={Uri.EscapeDataString(resetToken)}";

            await emailService.SendPasswordResetEmailAsync(normilizeEmail, resetUrl, ct);

            return AuthResult.Ok();
        }

        public async Task<AuthResult> ResetPasswordAsync(ResetPasswordDTO resetPasswordDTO, CancellationToken ct = default)
        {
            if (resetPasswordDTO == null || string.IsNullOrWhiteSpace(resetPasswordDTO.Token))
                return AuthResult.Fail("Invalid token.");

            var validationError = ValidatePassword(resetPasswordDTO.NewPassword);
            if (validationError != null) return AuthResult.Fail(validationError);

            var user = await bPFL_DBContext.Users
                .FirstOrDefaultAsync(u => u.PasswordResetToken == resetPasswordDTO.Token, ct);

            if (user == null || user.PasswordResetTokenExpires == null || user.PasswordResetTokenExpires < DateTime.UtcNow)
                return AuthResult.Fail("Invalid or expired reset token.");

            user.Password = BCrypt.Net.BCrypt.EnhancedHashPassword(resetPasswordDTO.NewPassword, 13);
            user.PasswordResetToken = null;
            user.PasswordResetTokenExpires = null;


            await bPFL_DBContext.RefreshTokens
                .Where(rt => rt.UserId == user.Id && rt.RevokedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(rt => rt.RevokedAt, DateTime.UtcNow), ct);

            await bPFL_DBContext.SaveChangesAsync(ct);

            return AuthResult.Ok();
        }

        internal string GenerateJwtToken(User user)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Email, user.Email),
                new(ClaimTypes.Role, user.Role ?? "User")
            };


            var credentials = new SigningCredentials(_jwtSecurityKey, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _jwtIssuer,
                audience: _jwtAudience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(_jwtExpirationMinutes),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private static string GenerateRefreshToken()
        {
            return Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        }

        private static string GenerateEmailToken()
        {
            return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        }

        private static string HashToken(string token)
        {
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        }

        internal static string NormalizeEmail(string email)
        {
            return email.Trim().ToLowerInvariant();
        }

        internal async Task<string> CreateAndStoreRefreshTokenAsync(User user, CancellationToken ct = default)
        {
            var rawRefreshToken = GenerateRefreshToken();
            var hashedRefreshToken = HashToken(rawRefreshToken);

            var refreshToken = new RefreshToken
            {
                UserId = user.Id,
                TokenHash = hashedRefreshToken,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(7)
            };

            bPFL_DBContext.RefreshTokens.Add(refreshToken);
            await bPFL_DBContext.SaveChangesAsync(ct);

            return rawRefreshToken;
        }

        private static string? ValidatePassword(string? password)
        {
            if (string.IsNullOrWhiteSpace(password))
                return "Password is required.";
            if (password.Length < 8)
                return "Password must be at least 8 characters.";
            if (!password.Any(char.IsUpper))
                return "Password must contain at least one uppercase letter.";
            if (!password.Any(char.IsLower))
                return "Password must contain at least one lowercase letter.";
            if (!password.Any(char.IsDigit))
                return "Password must contain at least one digit.";
            if (!password.Any(_specialChars.Contains))
                return "Password must contain at least one special character.";
            return null;
        }
    }
}
