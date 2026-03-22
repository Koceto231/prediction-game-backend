using BPFL.API.Services;

namespace BPFL.API.Exceptions
{
    public class LeagueException : Exception
    {
        public LeagueErrorType ErrorType { get; }
        public LeagueException(string message, LeagueErrorType errorType) : base(message)
            => ErrorType = errorType;
    }

}
