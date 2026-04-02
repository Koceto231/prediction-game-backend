using BPFL.API.Exceptions;
using BPFL.API.Models.DTO;
using BPFL.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BPFL.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class LeagueController : ControllerBase
    {
        private readonly LeagueService leagueService;

        public LeagueController(LeagueService _leagueService)
        {
            leagueService = _leagueService;
        }

        [HttpPost]

        public async Task<IActionResult> CreateLeague([FromBody] CreateLeagueDTO createLeagueDTO,CancellationToken ct =default)
        {
            var userIdClaim = GetUserId();
            if (userIdClaim == null)
            {
                return Unauthorized(new { message = "Invalid or missing ID claim" });
            }

            try
            {
                var league = await leagueService.CreateLeagueAsync(userIdClaim.Value, createLeagueDTO, ct);
                return Ok(league);

            }
            catch (LeagueException ex)
            {
                return ex.ErrorType switch
                {
                    LeagueErrorType.NotFound => NotFound(new { message = ex.Message }),
                    LeagueErrorType.NotOwner => Forbid(),
                    LeagueErrorType.AlreadyMember => Conflict(new { message = ex.Message }),
                    _ => BadRequest(new { message = ex.Message })
                };
            }



        }

        [HttpPost("join")]

        public async Task<IActionResult> JoinLeague([FromBody] JoinLeagueDTO joinLeagueDTO, CancellationToken ct = default)
        {
            var userIdClaim = GetUserId();
            if (userIdClaim == null)
            {
                return Unauthorized(new { message = "Invalid or missing ID claim" });
            }

            try
            {
                var result = await leagueService.JoinLeagueAsync(userIdClaim.Value, joinLeagueDTO, ct);
                return Ok(result);
            }
            catch (LeagueException ex)
            {
                return ex.ErrorType switch
                {
                    LeagueErrorType.NotFound => NotFound(new { message = ex.Message }),
                    LeagueErrorType.InvalidInviteCode => BadRequest(new { message = ex.Message }),
                    LeagueErrorType.AlreadyMember => Conflict(new { message = ex.Message }),
                    _ => BadRequest(new { message = ex.Message })
                };
            }

        }

        [HttpDelete("{leagueId}/leave")]
        public async Task<IActionResult> LeaveLeague(int leagueId, CancellationToken ct = default)
        {
            var userIdClaim = GetUserId();
            if (userIdClaim == null)
            {
                return Unauthorized(new { message = "Invalid or missing ID claim" });
            }

            try
            {
                await leagueService.LeaveLeagueAsync(userIdClaim.Value, leagueId, ct);
                return NoContent();
            }
            catch (LeagueException ex)
            {
                return ex.ErrorType switch
                {
                    LeagueErrorType.NotFound => NotFound(new { message = ex.Message }),
                    LeagueErrorType.NotMember => BadRequest(new { message = ex.Message }),
                    LeagueErrorType.CannotLeaveAsOwner => BadRequest(new { message = ex.Message }),
                    _ => BadRequest(new { message = ex.Message })
                };
            }
        }


        [HttpDelete("{leagueId}")]
        public async Task<IActionResult> DeleteLeague(int leagueId, CancellationToken ct = default)
        {
            var userIdClaim = GetUserId();
            if (userIdClaim == null)
            {
                return Unauthorized(new { message = "Invalid or missing ID claim" });
            }

            try
            {
                await leagueService.DeleteLeague(userIdClaim.Value, leagueId, ct);
                return NoContent();
            }
            catch (LeagueException ex)
            {
                return ex.ErrorType switch
                {
                    LeagueErrorType.NotFound => NotFound(new { message = ex.Message }),
                    LeagueErrorType.NotOwner => Forbid(),
                    _ => BadRequest(new { message = ex.Message })
                };
            }
        }

        [HttpGet("my")]
        public async Task<IActionResult> MyLeagues(CancellationToken ct = default)
        {
            var userIdClaim = GetUserId();
            if (userIdClaim == null)
            {
                return Unauthorized(new { message = "Invalid or missing ID claim" });
            }

            
                var result = await leagueService.GetMyLeages(userIdClaim.Value, ct);
                return Ok(result);
           
        }

        [HttpGet("{leagueId}/leaderboard")]

        public async Task<IActionResult> LeagueLeaderboard(int leagueId, CancellationToken ct = default)
        {
            var userIdClaim = GetUserId();
            if (userIdClaim == null)
            {
                return Unauthorized(new { message = "Invalid or missing ID claim" });
            }

            try
            {
                var result = await leagueService.GetLeagueLeaderboardsAsync(userIdClaim.Value, leagueId, ct);
                return Ok(result);
            }
            catch (LeagueException ex)
            {
                return ex.ErrorType switch
                {
                    LeagueErrorType.NotFound => NotFound(new { message = ex.Message }),
                    LeagueErrorType.NotMember => Forbid(),
                    _ => BadRequest(new { message = ex.Message })
                };
            }

        }

        private int? GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdClaim, out var id ) ? id : null;
        }
    }
}
