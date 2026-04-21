namespace BPFL.API.Modules.AI.Domain.Entities
{
    public class MatchProbabilityOutput
    {
        public int MatchId { get; set; }

        public decimal HomeWinProbability { get; set; }
        public decimal DrawProbability { get; set; }
        public decimal AwayWinProbability { get; set; }

        public decimal BTTSYesProbability { get; set; }
        public decimal BTTSNoProbability { get; set; }

        public decimal Over25Probability { get; set; }
        public decimal Under25Probability { get; set; }

        public decimal ConfidenceScore { get; set; }
    }
}
