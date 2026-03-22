using BPFL.API.Models.DTO;
using BPFL.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace BPFL.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AuthServices authServices;


        public AuthController(AuthServices _authServices)
        {
            authServices = _authServices;
            
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDTO registerDTO, CancellationToken ct = default)
        {

            var result = await authServices.RegisterAsync(registerDTO, ct);

            if (!result.Success)
            {
                return BadRequest(new { mesage = result.Error });
            }

            return Ok(result);
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDTO loginDto, CancellationToken ct = default)
        {

            var result = await authServices.LoginAsync(loginDto, ct);

            if (!result.Success)
            {
                return Unauthorized(new { message = result.Error });
            }

            return Ok(new
            {
                token = result.Token
            });
        }

    }
}
