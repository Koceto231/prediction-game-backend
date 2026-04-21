namespace BPFL.API.Modules.AI.Application.DTOs
{
    public class FinalPredictionResponse
    {
        public int MatchId { get; set; }

        public decimal HomeWinProbability { get; set; }
        public decimal DrawProbability { get; set; }
        public decimal AwayWinProbability { get; set; }

        public decimal BTTSYesProbability { get; set; }
        public decimal BTTSNoProbability { get; set; }

        public decimal Over25Probability { get; set; }
        public decimal Under25Probability { get; set; }

        public decimal OverCardsProbability { get; set; }
        public decimal UnderCardsProbability { get; set; }

        public decimal HomeMoreCardsProbability { get; set; }
        public decimal AwayMoreCardsProbability { get; set; }

        public decimal OverCornersProbability { get; set; }
        public decimal UnderCornersProbability { get; set; }

        public decimal HomeMoreCornersProbability { get; set; }
        public decimal AwayMoreCornersProbability { get; set; }

        public decimal OverShotsProbability { get; set; }
        public decimal UnderShotsProbability { get; set; }

        public decimal HomeMoreShotsProbability { get; set; }
        public decimal AwayMoreShotsProbability { get; set; }

        public string MostLikelyScorer { get; set; } = string.Empty;
        public decimal MostLikelyScorerProbability { get; set; }

        public string SecondMostLikelyScorer { get; set; } = string.Empty;
        public decimal SecondMostLikelyScorerProbability { get; set; }

        public decimal Confidence { get; set; }

        public string FormSummary { get; set; } = string.Empty;
        public string GoalsSummary { get; set; } = string.Empty;
        public string CardsSummary { get; set; } = string.Empty;
        public string CornersSummary { get; set; } = string.Empty;
        public string ShotsSummary { get; set; } = string.Empty;
        public string ScorerSummary { get; set; } = string.Empty;

        public string FinalSummary { get; set; } = string.Empty;
    }
}
