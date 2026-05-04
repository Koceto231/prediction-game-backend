using static BPFL.API.Models.Predictionenums;

namespace BPFL.API.Features.Predictions
{
    public class CreatePredictionDTO
    {
        public int MatchId { get; set; }

        public int? PredictionHomeScore { get; set; }

        public int? PredictionAwayScore { get; set; }

        public MatchWinner? PredictionWinner { get; set; }

        public bool? PredictionBTTS { get; set; }
        public OverUnderLine? PredictionOULine { get; set; }
        public OverUnderPick? PredictionOUPick { get; set; }
    }
}
