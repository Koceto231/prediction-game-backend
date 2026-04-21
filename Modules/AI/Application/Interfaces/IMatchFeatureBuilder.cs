using BPFL.API.Modules.AI.Domain.Entities;

namespace BPFL.API.Modules.AI.Application.Interfaces
{
    public interface IMatchFeatureBuilder
    {
        Task<MatchFeatureSet> BuildAsync(int matchId, CancellationToken ct = default);
    }
}
