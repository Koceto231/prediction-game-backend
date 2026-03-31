
using BCrypt.Net;
using BPFL.API.Data;
using BPFL.API.Models;
using BPFL.API.Models.DTO;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;


namespace BPFL.API.Services
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
        public static AuthResult Fail(string error) => new() { Success = false, Error = error };
    }

    public class AuthServices
    {
        private readonly BPFL_DBContext bPFL_DBContext;
        private readonly ILogger<AuthServices> _logger;
        private readonly IConfiguration configuration;
        private readonly EmailService emailService;



        public AuthServices(BPFL_DBContext _bPFLDBContext,IConfiguration _configuration, ILogger<AuthServices> logger, EmailService _emailService)
        {
            bPFL_DBContext = _bPFLDBContext;
            configuration = _configuration;
            _logger = logger;
            emailService = _emailService;
        }

        public async Task<AuthResult> RegisterAsync(RegisterDTO registerDTO, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(registerDTO);

            var emailValidator = new EmailAddressAttribute();

            if (string.IsNullOrWhiteSpace(registerDTO.Email) || !emailValidator.IsValid(registerDTO.Email))
                return AuthResult.Fail("Invalid email format.");

            if (string.IsNullOrWhiteSpace(registerDTO.Password))
                return AuthResult.Fail("Password is required.");

            if (registerDTO.Password.Length < 8)
                return AuthResult.Fail("Password must be at least 8 characters.");

            if (!registerDTO.Password.Any(char.IsUpper))
                return AuthResult.Fail("Password must contain at least one uppercase letter.");

            if (!registerDTO.Password.Any(char.IsLower))
                return AuthResult.Fail("Password must contain at least one lowercase letter.");

            if (!registerDTO.Password.Any(char.IsDigit))
                return AuthResult.Fail("Password must contain at least one digit.");

            if (!registerDTO.Password.Any(c => "!@#$%^&*()_+-=[]{}|;:,.<>?".Contains(c)))
                return AuthResult.Fail("Password must contain at least one special character.");

            if (string.IsNullOrWhiteSpace(registerDTO.Username) || registerDTO.Username.Length < 3)
                return AuthResult.Fail("Invalid username.");

            var normalizedEmail = NormalizeEmail(registerDTO.Email);

            if (await bPFL_DBContext.Users.AnyAsync(u => u.Email == normalizedEmail, ct))
                return AuthResult.Fail("Email already exists.");

            string passwordHash = BCrypt.Net.BCrypt.EnhancedHashPassword(registerDTO.Password, 13);

            var user = new User
            {
                Email = normalizedEmail,
                Username = registerDTO.Username.Trim(),
                Password = passwordHash,
                Role = "User"
            };

            var emailToken = GenerateEmailToken();

            user.EmailVerificationToken = emailToken;
            user.EmailVerificationTokenExpires = DateTime.UtcNow.AddMinutes(60);
            user.IsEmailVerified = false;
            bPFL_DBContext.Users.Add(user);
            await bPFL_DBContext.SaveChangesAsync(ct);

            var verifyUrl = $"https://localhost:5173/verify-email?token={Uri.EscapeDataString(emailToken)}";

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

           if (string.IsNullOrWhiteSpace(loginDTO.Email) || string.IsNullOrWhiteSpace(loginDTO.Password)) { 
                return AuthResult.Fail("Invalid credentials.");
            }

            var normalizedEmail = NormalizeEmail(loginDTO.Email);


            var user = await bPFL_DBContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == normalizedEmail,ct);

            if (user == null)
            {
                _logger.LogWarning("Failed login attempt for email: {Email}", normalizedEmail);
                return AuthResult.Fail("Invalid credentials.");
            }


            if (!user.IsEmailVerified)
            {
                return AuthResult.Fail("Please verify your email first.");
            }

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

            return AuthResult.Ok(new AuthTokenDTO
            {
                AccessToken = accesstoken,
                RefreshToken = refreshToken
            });

        }

        public async Task<AuthResult> RefreshTokenAsync(string rawRefreshToken,CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(rawRefreshToken))
                return AuthResult.Fail("Invalid refresh token.");

            var hashedToken = HashToken(rawRefreshToken);

            var storedToken = await bPFL_DBContext.RefreshTokens.Include(t => t.User).FirstOrDefaultAsync(t => t.TokenHash == hashedToken, ct);

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

            var user = await bPFL_DBContext.Users.FirstOrDefaultAsync(c => c.EmailVerificationToken == emailToken, ct);

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
            {
                return AuthResult.Fail("Email is required.");
            }

            var normilizeEmail = NormalizeEmail(forgotPaswwordDTO.Email);

            var user = await bPFL_DBContext.Users.FirstOrDefaultAsync(u => u.Email == normilizeEmail, ct);

            if (user == null)
            {
                return AuthResult.Ok();
            }

            if ( string.IsNullOrWhiteSpace(user.Email))
            {
                return AuthResult.Fail("Invalid email address.");
            }

            var resetToken = GenerateEmailToken();

            user.PasswordResetToken = resetToken;
            user.PasswordResetTokenExpires = DateTime.UtcNow.AddMinutes(30);

            await bPFL_DBContext.SaveChangesAsync(ct);


            var frontendBaseUrl = configuration["App:FrontendBaseUrl"] ?? "https://localhost:5173";
            var resetUrl = $"{frontendBaseUrl}/reset-password?token={Uri.EscapeDataString(resetToken)}";

            _logger.LogInformation("Forgot password requested. DTO email: {DtoEmail}", forgotPaswwordDTO.Email);
            _logger.LogInformation("Normalized email: {NormalizedEmail}", normilizeEmail);
            _logger.LogInformation("User found: {Found}, User email: {UserEmail}", user != null, user?.Email);

            await emailService.SendPasswordResetEmailAsync(normilizeEmail, resetUrl,ct);

            return AuthResult.Ok();

        }

        public async Task<AuthResult> ResetPasswordAsync(ResetPasswordDTO resetPasswordDTO, CancellationToken ct = default)
        {
            if (resetPasswordDTO == null || string.IsNullOrWhiteSpace(resetPasswordDTO.Token))
                return AuthResult.Fail("Invalid token.");

            if (string.IsNullOrWhiteSpace(resetPasswordDTO.NewPassword))
                return AuthResult.Fail("New password is required.");

            if (resetPasswordDTO.NewPassword.Length < 8)
                return AuthResult.Fail("Password must be at least 8 characters.");

            if (!resetPasswordDTO.NewPassword.Any(char.IsUpper))
                return AuthResult.Fail("Password must contain at least one uppercase letter.");

            if (!resetPasswordDTO.NewPassword.Any(char.IsLower))
                return AuthResult.Fail("Password must contain at least one lowercase letter.");

            if (!resetPasswordDTO.NewPassword.Any(char.IsDigit))
                return AuthResult.Fail("Password must contain at least one digit.");

            if (!resetPasswordDTO.NewPassword.Any(c => "!@#$%^&*()_+-=[]{}|;:,.<>?".Contains(c)))
                return AuthResult.Fail("Password must contain at least one special character.");

            var user = await bPFL_DBContext.Users.FirstOrDefaultAsync(u => u.PasswordResetToken == resetPasswordDTO.Token, ct);

            if (user == null)
            {
                return AuthResult.Fail("Invalid or expired reset token.");
            }

            if (user.PasswordResetTokenExpires == null || user.PasswordResetTokenExpires < DateTime.UtcNow)
                return AuthResult.Fail("Invalid or expired reset token.");

            user.Password = BCrypt.Net.BCrypt.EnhancedHashPassword(resetPasswordDTO.NewPassword, 13);
            user.PasswordResetToken = null;
            user.PasswordResetTokenExpires = null;

            var userRefreshTokens = await bPFL_DBContext.RefreshTokens.Where(rt => rt.UserId == user.Id && rt.RevokedAt == null)
                .ToListAsync();

            foreach (var refreshToken in userRefreshTokens)
            {
                refreshToken.RevokedAt = DateTime.UtcNow;
            }

            await bPFL_DBContext.SaveChangesAsync(ct);

            return AuthResult.Ok();
        }

        internal string GenerateJwtToken(User user)
        {
            var claims = new List<Claim>
            {
              new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
              new Claim(ClaimTypes.Email, user.Email),
              new Claim(ClaimTypes.Role, user.Role ?? "User")

            };

            string? secketKey = configuration.GetSection("Jwt").GetSection("Key").Value;


            if (string.IsNullOrEmpty(secketKey))
            {
                throw new Exception("JWT Key is not configured.");
            }

            byte[]? stringToByte = Encoding.UTF8.GetBytes(secketKey);
            var key = new SymmetricSecurityKey(stringToByte);

            var credetentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var expirationMinutes = int.Parse(configuration.GetSection("Jwt").GetSection("ExpirationMinutes").Value ?? "15");

            string issuer = configuration.GetSection("Jwt").GetSection("Issuer").Value!;

            string audience = configuration.GetSection("Jwt").GetSection("Audience").Value!;


            var token = new JwtSecurityToken(

                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(expirationMinutes),
                signingCredentials: credetentials
                );

            var tokenHandler = new JwtSecurityTokenHandler();
            string jwtToken = tokenHandler.WriteToken(token);

            return jwtToken; 

        }

        private static string GenerateRefreshToken()
        {
            var bytes = RandomNumberGenerator.GetBytes(64);
            return Convert.ToBase64String(bytes);
        }

        private static string GenerateEmailToken()
        {
            var bytes = RandomNumberGenerator.GetBytes(32);
            return Convert.ToBase64String(bytes);
        }

        private static string HashToken(string token)
        {
            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(token);
            return Convert.ToHexString(sha.ComputeHash(bytes));
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
    }
}
