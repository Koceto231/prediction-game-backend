using BPFL.API.Exceptions;

using System.Net;
using System.Text.Json;

namespace BPFL.API.Middleware
{
    public class ExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionMiddleware> _logger;

        public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }


        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                   "Unhandled exception. TraceId: {TraceId}, Path: {Path}",
                   context.TraceIdentifier,
                   context.Request.Path);

                await HandleExeptionAsync(context, ex);
            }
        }

        private static async Task HandleExeptionAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = "application/json";

            int statusCode = exception switch
            {
                ArgumentException => (int)HttpStatusCode.BadRequest,
                UnauthorizedAccessException => (int)HttpStatusCode.Unauthorized,
                KeyNotFoundException => (int)HttpStatusCode.NotFound,
                BPFLDataClientException ex => (int)ex.StatusCode,

                PredictionException predictionException => predictionException.ErrorType switch
                {
                    PredictionErrorType.MatchNotFound => (int)HttpStatusCode.NotFound,
                    PredictionErrorType.PredictionNotFound => (int)HttpStatusCode.NotFound,
                    PredictionErrorType.MatchAlreadyStarted => (int)HttpStatusCode.Conflict,
                    PredictionErrorType.PredictionAlreadyExists => (int)HttpStatusCode.Conflict,
                    PredictionErrorType.InvalidInput => (int)HttpStatusCode.BadRequest,
                    _ => (int)HttpStatusCode.BadRequest
                },

                ScoringException scoringException => scoringException.ErrorType switch
                {
                    ScoringErrorType.MatchNotFound => (int)HttpStatusCode.NotFound,
                    ScoringErrorType.MatchNotFinished => (int)HttpStatusCode.Conflict,
                    ScoringErrorType.ScoreNotAvailable => (int)HttpStatusCode.Conflict,
                    _ => (int)HttpStatusCode.BadRequest
                },

                LeagueException leagueException => leagueException.ErrorType switch
                {
                    LeagueErrorType.NotFound => (int)HttpStatusCode.NotFound,

                    LeagueErrorType.InvalidInviteCode => (int)HttpStatusCode.BadRequest,

                    LeagueErrorType.AlreadyMember => (int)HttpStatusCode.BadRequest,

                    LeagueErrorType.NotMember => (int)HttpStatusCode.Forbidden,

                    LeagueErrorType.NotOwner => (int)HttpStatusCode.Forbidden,

                    LeagueErrorType.NameRequired => (int)HttpStatusCode.BadRequest,

                    LeagueErrorType.CannotLeaveAsOwner => (int)HttpStatusCode.BadRequest,

                    _ => (int)HttpStatusCode.BadRequest,
                },

                _ => (int)HttpStatusCode.InternalServerError
            };

            context.Response.StatusCode = statusCode;

            var response = new ErrorResponseDto
            {
                Status = statusCode,
                Error = exception.GetType().Name,
                Message = exception.Message,
                TraceId = context.TraceIdentifier
            };

            var json = JsonSerializer.Serialize(response);

            await context.Response.WriteAsync(json);
        }

        private sealed class ErrorResponseDto
        {
            public int Status { get; set; }
            public string Error { get; set; } = string.Empty;
            public string Message { get; set; } = string.Empty;
            public string TraceId { get; set; } = string.Empty;
        }
    }
}