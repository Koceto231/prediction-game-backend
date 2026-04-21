using BPFL.API.Modules.AI.Application.DTOs;
using BPFL.API.Modules.AI.Application.Interfaces;

namespace BPFL.API.Modules.AI.Application.UseCases
{
    public class GetMatchContext
    {
        private readonly IMatchContextRepository matchContextRepository;

        public GetMatchContext(IMatchContextRepository _matchContextRepository)
        {
            matchContextRepository = _matchContextRepository;
        }

        public async Task<MatchContextResponse> ExecuteAsync(int matchId, CancellationToken ct = default)
        {
            if (matchId <= 0)
                throw new ArgumentOutOfRangeException(nameof(matchId));

            var matchContext = await matchContextRepository.GetByMatchIdAsync(matchId, ct);

            if (matchContext == null)
                throw new KeyNotFoundException($"Match with id {matchId} was not found.");

            return matchContext;
        }
    }
}
