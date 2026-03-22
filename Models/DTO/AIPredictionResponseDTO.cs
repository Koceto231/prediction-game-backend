namespace BPFL.API.Models.DTO
{
    public class AIPredictionResponseDTO
    {
        public double PredictedHomeScore { get; set; }

        public double PredictedAwayScore { get; set; }

        public string Pick { get; set; } = null!;

        public double Confidence { get; set; }

        public double HomeWinProbability { get; set; }
        public double DrawProbability { get; set; }
        public double AwayWinProbability { get; set; }
    }
}
