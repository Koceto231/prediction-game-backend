using BPFL.API.Modules.AI.Application.DTOs;
using BPFL.API.Modules.AI.Applications.Interfaces;
using BPFL.API.Modules.AI.Infrastructures.Repositories;

namespace BPFL.API.Modules.AI.Application.UseCases
{
    public class GetHeadToHead
    {
        private readonly IHeadToHeadRepository headToHeadRepository;

        public GetHeadToHead(IHeadToHeadRepository _headToHeadRepository)
        {
            headToHeadRepository = _headToHeadRepository;
        }

        public async Task<HeadToHeadResponse> ExecuteAsync(
           int homeTeamId,
           int awayTeamId,
           CancellationToken ct = default)
        {
            if (homeTeamId <= 0)
                throw new ArgumentOutOfRangeException(nameof(homeTeamId));

            if (awayTeamId <= 0)
                throw new ArgumentOutOfRangeException(nameof(awayTeamId));

            var matches = await headToHeadRepository.GetRecentAsync(homeTeamId, awayTeamId, ct);

            return new HeadToHeadResponse
            {
                HomeTeamId = homeTeamId,
                AwayTeamId = awayTeamId,
                Matches = matches
            };
        }
    }
}
