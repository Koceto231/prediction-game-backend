using static BPFL.API.Models.Predictionenums;

namespace BPFL.API.Features.Predictions
{
    public class PredictionResponseDTO
    {
        public int Id { get; set; }

        public int MatchId { get; set; }

        public string HomeTeam { get; set; } = null!;

        public int? PredictedHomeScore { get; set; }

        public string AwayTeam { get; set; } = null!;

        public int? PredictedAwayScore { get; set; }

        public MatchWinner? PredictionWinner { get; set; }

        public bool? PredictionBTTS { get; set; }

        public OverUnderLine? PredictionOULine { get; set; }

        public OverUnderPick? PredictionOUPick { get; set; }

        public int? Points { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
