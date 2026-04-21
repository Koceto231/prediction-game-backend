using BPFL.API.Data;
using BPFL.API.Models;
using BPFL.API.Models.DTO;
using BPFL.API.Modules.Wallet.Domain.Entities;
using Google.Apis.Auth;
using Microsoft.EntityFrameworkCore;

namespace BPFL.API.Services
{
    public class GoogleAuthService
    {
        private readonly BPFL_DBContext bPFL_DBContext;
        private readonly ILogger<GoogleAuthService> logger;
        private readonly AuthServices authServices;
        private readonly IConfiguration configuration;

        public GoogleAuthService(BPFL_DBContext _bPFL_DBContext, ILogger<GoogleAuthService> _logger,
            AuthServices _authServices, IConfiguration _configuratio)
        {
            bPFL_DBContext = _bPFL_DBContext;
            authServices = _authServices;
            configuration = _configuratio;
            logger = _logger;
        }

        public async Task<AuthResult> LoginWithGoogleAsync(string idToken, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(idToken))
            {
                return AuthResult.Fail("Google Id token is required");
            }

            GoogleJsonWebSignature.Payload payload;
            try
            {
                var clientId = configuration["Google:ClientId"];
                if (string.IsNullOrEmpty(clientId))
                {
                    throw new InvalidOperationException("Google:ClientId is not configured.");
                }

                var settings = new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = new[] { clientId }
                };
                logger.LogInformation("Google ClientId: {ClientId}", clientId);
                payload = await GoogleJsonWebSignature.ValidateAsync(idToken, settings);

            }
            catch (InvalidJwtException ex)
            {
                logger.LogWarning("Invalid Google token: {Message}", ex.Message);
                return AuthResult.Fail("Invalid Google token.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Google token validation failed.");
                return AuthResult.Fail("Google authentication failed.");
            }

            var user = await FindOrCreate(payload, ct);

            var accesstoken = authServices.GenerateJwtToken(user);
            var refreshToken = await authServices.CreateAndStoreRefreshTokenAsync(user, ct);

            logger.LogInformation(
                "Google login successful for user {UserId} ({Email})",
                user.Id, user.Email);

            return AuthResult.Ok(new AuthTokenDTO
            {
                AccessToken = accesstoken,
                RefreshToken = refreshToken
            });


        }

        private async Task<User> FindOrCreate(GoogleJsonWebSignature.Payload payload, CancellationToken ct = default)
        {
            var user = await bPFL_DBContext.Users.FirstOrDefaultAsync(x => x.GoogleId == payload.Subject, ct);
            if (user != null)
            {
                await EnsureWalletExistsAsync(user.Id, ct);
                return user;
            }

            var normilizedEmail = AuthServices.NormalizeEmail(payload.Email);

            user = await bPFL_DBContext.Users.FirstOrDefaultAsync(u => u.Email == normilizedEmail, ct);

            if (user != null)
            {
                user.GoogleId = payload.Subject;
                await bPFL_DBContext.SaveChangesAsync(ct);

                await EnsureWalletExistsAsync(user.Id, ct);


                logger.LogInformation(
                   "Linked Google account to existing user {UserId}", user.Id);

                return user;

            }

            var username = await GenerateUniqueUsernameAsync(payload.Name, ct);

            user = new User
            {
                Username = username,
                Email = payload.Email,
                GoogleId = payload.Subject,
                Password = null
            };

            bPFL_DBContext.Users.Add(user);
            await bPFL_DBContext.SaveChangesAsync(ct);

            var wallet = new Wallet 
            {
                UserId = user.Id,
                Balance = 1000m,
                StartingBalance = 1000m,
                UpdatedAt = DateTime.UtcNow

            };

            await bPFL_DBContext.Wallets.AddAsync(wallet, ct);
            await bPFL_DBContext.SaveChangesAsync(ct);

            logger.LogInformation(
                "Auto-registered new user {UserId} via Google ({Email})",
                user.Id, user.Email);

            return user;


        }

        private async Task EnsureWalletExistsAsync(int userId, CancellationToken ct = default)
        {
            var existingWallet = await bPFL_DBContext.Wallets
                .FirstOrDefaultAsync(w => w.UserId == userId, ct);

            if (existingWallet != null)
                return;

            var wallet = new Wallet
            {
                UserId = userId,
                Balance = 1000m,
                StartingBalance = 1000m,
                UpdatedAt = DateTime.UtcNow
            };

            await bPFL_DBContext.Wallets.AddAsync(wallet, ct);
            await bPFL_DBContext.SaveChangesAsync(ct);
        }
        private string TransliterateBulgarianToLatin(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "user";

            var map = new Dictionary<char, string>
            {
                ['а'] = "a",
                ['б'] = "b",
                ['в'] = "v",
                ['г'] = "g",
                ['д'] = "d",
                ['е'] = "e",
                ['ж'] = "zh",
                ['з'] = "z",
                ['и'] = "i",
                ['й'] = "y",
                ['к'] = "k",
                ['л'] = "l",
                ['м'] = "m",
                ['н'] = "n",
                ['о'] = "o",
                ['п'] = "p",
                ['р'] = "r",
                ['с'] = "s",
                ['т'] = "t",
                ['у'] = "u",
                ['ф'] = "f",
                ['х'] = "h",
                ['ц'] = "ts",
                ['ч'] = "ch",
                ['ш'] = "sh",
                ['щ'] = "sht",
                ['ъ'] = "a",
                ['ь'] = "y",
                ['ю'] = "yu",
                ['я'] = "ya"
            };

            var result = new System.Text.StringBuilder();

            foreach (var c in text.ToLower())
            {
                if (map.ContainsKey(c))
                    result.Append(map[c]);
                else
                    result.Append(c);
            }

            return result.ToString();
        }

        private async Task<string> GenerateUniqueUsernameAsync(
    string googleName, CancellationToken ct)
        {
            var transliterated = TransliterateBulgarianToLatin(googleName);

            var baseUsername = (transliterated ?? "user")
                .ToLower()
                .Replace(" ", ".")
                .Replace("-", ".")
                .Where(c => char.IsLetterOrDigit(c) || c == '.')
                .Take(30)
                .Aggregate(string.Empty, (acc, c) => acc + c);

            if (string.IsNullOrEmpty(baseUsername))
                baseUsername = "user";

            if (!await bPFL_DBContext.Users.AnyAsync(u => u.Username == baseUsername, ct))
                return baseUsername;

            for (int i = 2; i <= 999; i++)
            {
                var candidate = $"{baseUsername}{i}";
                if (!await bPFL_DBContext.Users.AnyAsync(u => u.Username == candidate, ct))
                    return candidate;
            }

            return $"{baseUsername}_{Guid.NewGuid().ToString("N")[..6]}";
        }

    }
}
