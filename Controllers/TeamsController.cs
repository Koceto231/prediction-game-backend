using BPFL.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace BPFL.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TeamsController : ControllerBase
    {
       private readonly TeamService teamService;

        public TeamsController(TeamService _teamService)
        {
            teamService = _teamService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllAsync()
        {
            var result = await teamService.GetAllAsync();
            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetByIdAsync(int id)
        {
            var result = await teamService.GetByIdAsync(id);
            if (result == null)
            {
                return NotFound();
            }
            return Ok(result);
        }

    }
}
