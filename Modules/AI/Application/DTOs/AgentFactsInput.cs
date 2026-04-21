using BPFL.API.Modules.AI.Domain.Entities;

namespace BPFL.API.Modules.AI.Application.DTOs
{
    public class AgentFactsInput
    {
        public MatchContextResponse MatchContext { get; set; } = new();
        public MatchFeatureSet Features { get; set; } = new();
        public HeadToHeadResponse HeadToHead { get; set; } = new();
    }
}
