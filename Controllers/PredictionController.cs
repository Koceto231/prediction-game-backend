using BPFL.API.Exceptions;
using BPFL.API.Models.DTO;
using BPFL.API.Services;
using BPFL.API.Services.Agents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BPFL.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class PredictionController : ControllerBase
    {
        private readonly PredictionService predictionService;
        private readonly OpenRouterClient openRouterClient;

        public PredictionController(PredictionService _predictionService, OpenRouterClient _openRouterClient)
        {
            predictionService = _predictionService;
            openRouterClient = _openRouterClient;
        }

        [HttpGet("ai-status")]
        public async Task<IActionResult> CheckAiStatus(CancellationToken ct)
        {
            try
            {
                var result = await openRouterClient.CompleteAsync(
                    "You are a test assistant.",
                    "Reply with the single word: OK",
                    ct);
                return Ok(new { status = "connected", response = result });
            }
            catch (Exception ex)
            {
                return Ok(new { status = "failed", error = ex.Message, inner = ex.InnerException?.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> PostPrediction([FromBody] CreatePredictionDTO createPredictionDTO, CancellationToken ct)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized(new { message = "Invalid or missing ID claim" });
            }

            try
            {
                var result = await predictionService.CreatePredictionAsync(userId, createPredictionDTO, ct);
                return Ok(result);
            }
            catch (PredictionException ex)
            {
                return ex.ErrorType switch
                {
                    PredictionErrorType.MatchNotFound => NotFound(new { message = ex.Message }),
                    PredictionErrorType.PredictionNotFound => NotFound(new { message = ex.Message }),
                    PredictionErrorType.MatchAlreadyStarted => BadRequest(new { message = ex.Message }),
                    PredictionErrorType.PredictionAlreadyExists => BadRequest(new { message = ex.Message }),
                    PredictionErrorType.InvalidInput => BadRequest(new { message = ex.Message }),
                    _ => BadRequest(new { message = ex.Message })
                };
            }
            catch (DbUpdateException ex)
            {
                return StatusCode(500, new
                {
                    Status = 500,
                    Error = "DbUpdateException",
                    Message = ex.Message,
                    Inner = ex.InnerException?.Message,
                    TraceId = HttpContext.TraceIdentifier
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    Status = 500,
                    Error = ex.GetType().Name,
                    Message = ex.Message,
                    Inner = ex.InnerException?.Message,
                    TraceId = HttpContext.TraceIdentifier
                });
            }
        }

        [HttpGet("me")]
        public async Task<IActionResult> GetMyPredictionsAsync(CancellationToken ct)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized(new { message = "Invalid or missing ID claim" });
            }

            try
            {
                var res = await predictionService.GetMyPredictionsAsync(userId, ct);
                return Ok(res);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    Status = 500,
                    Error = ex.GetType().Name,
                    Message = ex.Message,
                    Inner = ex.InnerException?.Message,
                    TraceId = HttpContext.TraceIdentifier
                });
            }
        }

        [HttpPut("{matchId:int}")]
        public async Task<IActionResult> UpdatePrediction(
            [FromRoute] int matchId,
            [FromBody] CreatePredictionDTO createPredictionDTO,
            CancellationToken ct)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized(new { message = "Invalid or missing ID claim" });
            }

            try
            {
                var res = await predictionService.UpdatePredictionAsync(matchId, userId, createPredictionDTO, ct);
                return Ok(res);
            }
            catch (PredictionException ex)
            {
                return ex.ErrorType switch
                {
                    PredictionErrorType.MatchNotFound => NotFound(new { message = ex.Message }),
                    PredictionErrorType.PredictionNotFound => NotFound(new { message = ex.Message }),
                    PredictionErrorType.MatchAlreadyStarted => BadRequest(new { message = ex.Message }),
                    PredictionErrorType.PredictionAlreadyExists => BadRequest(new { message = ex.Message }),
                    PredictionErrorType.InvalidInput => BadRequest(new { message = ex.Message }),
                    _ => BadRequest(new { message = ex.Message })
                };
            }
            catch (DbUpdateException ex)
            {
                return StatusCode(500, new
                {
                    Status = 500,
                    Error = "DbUpdateException",
                    Message = ex.Message,
                    Inner = ex.InnerException?.Message,
                    TraceId = HttpContext.TraceIdentifier
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    Status = 500,
                    Error = ex.GetType().Name,
                    Message = ex.Message,
                    Inner = ex.InnerException?.Message,
                    TraceId = HttpContext.TraceIdentifier
                });
            }
        }
    }
}
