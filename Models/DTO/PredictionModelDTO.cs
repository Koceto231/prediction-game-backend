namespace BPFL.API.Models.DTO
{
    public class PredictionModelDTO
    {
        public double HomeStrength { get; set; }

        public double AwayStrength { get; set; }

        public double ExpectedHomeGoals { get; set; }

        public double ExpectedAwayGoals { get; set; }

        public double HomeWinProbavility { get; set; }

        public double DrawProbability { get; set; }

        public double AwayWinProbability { get; set; }
    }
}
