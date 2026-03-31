using BPFL.API.Services;

namespace BPFL.API.Exceptions
{
    
        public class ScoringException : AppException
        {
            public ScoringErrorType ErrorType { get; }
            public ScoringException(string message, ScoringErrorType scoringErrorType) : base(message,GetStatusCode(scoringErrorType))
            {
                ErrorType = scoringErrorType;
            }
            
        private static int GetStatusCode(ScoringErrorType scoringErrorType)
        {
            return scoringErrorType switch
            {
                ScoringErrorType.MatchNotFound => StatusCodes.Status404NotFound,
                ScoringErrorType.MatchNotFinished => StatusCodes.Status409Conflict,
                ScoringErrorType.ScoreNotAvailable => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status400BadRequest,
            };
        }

        }
    
}
