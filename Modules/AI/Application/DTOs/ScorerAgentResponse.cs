namespace BPFL.API.Modules.AI.Application.DTOs
{
    public class ScorerAgentResponse
    {
        public string MostLikelyScorer { get; set; } = string.Empty;
        public decimal MostLikelyScorerProbability { get; set; }

        public string SecondMostLikelyScorer { get; set; } = string.Empty;
        public decimal SecondMostLikelyScorerProbability { get; set; }

        public decimal Confidence { get; set; }

        public string Summary { get; set; } = string.Empty;
    }
}
