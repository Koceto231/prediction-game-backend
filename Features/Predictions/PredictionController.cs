using BPFL.API.Exceptions;
using BPFL.API.Features.Predictions;
using BPFL.API.Features.Matches;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BPFL.API.Features.Predictions
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class PredictionController : ControllerBase
    {
        private readonly PredictionService predictionService;
        private readonly OpenRouterClient openRouterClient;
        private readonly AIPredictionService aiPredictionService;
        private readonly MatchAnalysisService matchAnalysisService;
        private readonly PredictionModelService predictionModelService;

        public PredictionController(
            PredictionService _predictionService,
            OpenRouterClient _openRouterClient,
            AIPredictionService _aiPredictionService,
            MatchAnalysisService _matchAnalysisService,
            PredictionModelService _predictionModelService)
        {
            predictionService = _predictionService;
            openRouterClient = _openRouterClient;
            aiPredictionService = _aiPredictionService;
            matchAnalysisService = _matchAnalysisService;
            predictionModelService = _predictionModelService;
        }

        /// <summary>Returns an AI analysis for a match without saving a prediction.</summary>
        [HttpGet("analysis/{matchId:int}")]
        public async Task<IActionResult> GetMatchAnalysis(int matchId, CancellationToken ct)
        {
            try
            {
                var match = await predictionService.GetMatchForAnalysisAsync(matchId, ct);
                if (match == null) return NotFound(new { message = "Match not found." });

                var analysis = await matchAnalysisService.AnalyzeMatch(match, ct);
                var model = predictionModelService.BuildModel(analysis);
                var ai = await aiPredictionService.AIBuildPredictionAsync(analysis, model, ct);
                return Ok(ai);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
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
