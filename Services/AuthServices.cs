
using BPFL.API.Data;
using BPFL.API.Models;
using BPFL.API.Models.DTO;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace BPFL.API.Services
{

    public class AuthResult
    {
        public bool Success { get; private set; }
        public string? Token { get; private set; }
        public string? Error { get; private set; }
        public UserResponseDTO? User { get; private set; }
        public static AuthResult Ok(string token) => new() { Success = true, Token = token };
        public static AuthResult Ok(UserResponseDTO user) => new() { Success = true, User = user };
        public static AuthResult Fail(string error) => new() { Success = false, Error = error };
    }

    public class AuthServices
    {
        private readonly BPFL_DBContext bPFL_DBContext;
        private readonly ILogger<AuthServices> _logger;
        private readonly IConfiguration configuration;



        public AuthServices(BPFL_DBContext _bPFLDBContext,IConfiguration _configuration, ILogger<AuthServices> logger)
        {
            bPFL_DBContext = _bPFLDBContext;
            configuration = _configuration;
            _logger = logger;
        }

        public async Task<AuthResult> RegisterAsync(RegisterDTO registerDTO, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(registerDTO);

            if (string.IsNullOrWhiteSpace(registerDTO.Email))
                return AuthResult.Fail("Email is required.");

            if (string.IsNullOrWhiteSpace(registerDTO.Password))
                return AuthResult.Fail("Password is required."); 

            if (registerDTO.Password.Length < 8) 
                return AuthResult.Fail("Password must be at least 8 characters."); 

            if (string.IsNullOrWhiteSpace(registerDTO.Username))
                return AuthResult.Fail("Username is required.");

            if (await bPFL_DBContext.Users.AnyAsync(u => u.Email == registerDTO.Email, ct))
            {
                _logger.LogWarning("Registration attempt with existing email: {Email}", registerDTO.Email);
                return AuthResult.Fail("Email already exists.");

            }

            string PasswordHash = BCrypt.Net.BCrypt.EnhancedHashPassword(registerDTO.Password, 13);

            var user = new User {

            Email = registerDTO.Email,
            Username = registerDTO.Username,
            Password = PasswordHash,
            
            };

            bPFL_DBContext.Users.Add(user);
            await bPFL_DBContext.SaveChangesAsync(ct);

            _logger.LogInformation("New user registered: {UserId}", user.Id);

            return AuthResult.Ok(new UserResponseDTO { 
            
            Id = user.Id,
            Email = user.Email,
            Username = user.Username,
            Role = "User",
            
            });
        }

        public async Task<AuthResult> LoginAsync(LoginDTO loginDTO, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(loginDTO);

                if (string.IsNullOrWhiteSpace(loginDTO.Email) || string.IsNullOrWhiteSpace(loginDTO.Password)) { 
                return AuthResult.Fail("Invalid credentials.");
            }

            var user = await bPFL_DBContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == loginDTO.Email);
            var isValidPassword = BCrypt.Net.BCrypt.EnhancedVerify(loginDTO.Password, user?.Password);

            if (user == null || !isValidPassword)
            {
                _logger.LogWarning("Failed login attempt for email: {Email}", loginDTO.Email);
                return AuthResult.Fail("Invalid credentials.");
            }

            var token = GenerateJwtToken(user);

            _logger.LogInformation("User logged in: {UserId}", user.Id);

            return AuthResult.Ok(token);

        }

       
        private string GenerateJwtToken(User user)
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

            var expirationMinutes = int.Parse(configuration.GetSection("Jwt").GetSection("ExpirationMinutes").Value ?? "60");

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

    }
}
