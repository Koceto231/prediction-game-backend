

namespace BPFL.API.Exceptions
{
    public class PredictionException : AppException
    {
        public PredictionErrorType ErrorType { get; }
        public PredictionException(string message, PredictionErrorType predictionErrorType)
            : base(message,GetStatusCode(predictionErrorType))
        {
            ErrorType = predictionErrorType;
        }

        private static int GetStatusCode(PredictionErrorType predictionErrorType)
        {
            return predictionErrorType switch
            {
                PredictionErrorType.MatchAlreadyStarted => StatusCodes.Status409Conflict,
                PredictionErrorType.MatchNotFound => StatusCodes.Status404NotFound,
                PredictionErrorType.PredictionNotFound => StatusCodes.Status404NotFound,
                PredictionErrorType.PredictionAlreadyExists => StatusCodes.Status409Conflict,
                PredictionErrorType.InvalidInput => StatusCodes.Status400BadRequest,
                _ => StatusCodes.Status400BadRequest,

            };

        }
    }
}
