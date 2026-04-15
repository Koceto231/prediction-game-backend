using static BPFL.API.Models.Predictionenums;

namespace BPFL.API.Models
{
    public class Prediction
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int MatchId { get; set; }
        public int? PredictionHomeScore { get; set; }
        public int? PredictionAwayScore { get; set; }
        public bool? isCorrect { get; set; }
        public int? Points { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public User User { get; set; } = null!;
        public Match Match { get; set; } = null!;

        public MatchWinner? PredictionWinner { get; set; }   
        public MatchWinner? ActualWinner { get; set; }  
        public int? BonusPointsWinner { get; set; }  

        public bool? PredictionBTTS { get; set; }
        public bool? IsCorrectBTTS { get; set; }
        public int? PointsFromBTTS { get; set; }

        public OverUnderLine? PredictionOULine { get; set; }  
        public OverUnderPick? PredictionOUPick { get; set; }  
        public bool? ActualOUResult { get; set; }  
        public int? BonusPointsOU { get; set; }








    }
}
