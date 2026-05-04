namespace BPFL.API.Features.Profile
{
    public class ProfileStatsDTO
    {
        public int TotalPredictions { get; set; }
        public int ScoredPredictions { get; set; }
        public int TotalPoints { get; set; }
        public int ExactScoreCount { get; set; }
        public int CorrectOutcomeCount { get; set; }
        public double AccuracyPercent { get; set; }
        public int LeaguesCount { get; set; }
    }
}
