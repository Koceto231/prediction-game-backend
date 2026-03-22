namespace BPFL.API.Models.MatchAnalysis
{
    public class MatchAnalysisDTO
    {
        public string HomeTeam { get; set; } = null!;

        public string AwayTeam { get; set; } = null!;

        public DateTime MatchDate { get; set; }

        public int HomeRecentFromPoints { get; set; }

        public double HomeAverageGoalsScored { get; set; }

        public double HomeAverageGoalsConceded { get; set; }

        public double HomeAverageGoalsAtHome { get; set; }

        public int AwayRecentFromPoints { get; set; }

        public double AwayAverageGoalsScored { get; set; }

        public double AwayAverageGoalsConceded { get; set; }

        public double AwayAverageGoalsAtAway { get; set; }
        public int HomeMatchesAnalyzed { get; set; }
        public int AwayMatchesAnalyzed { get; set; }

    }
}
