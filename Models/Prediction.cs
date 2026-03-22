namespace BPFL.API.Models
{
    public class Prediction
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int MatchId { get; set; }
        public int PredictionHomeScore { get; set; }
        public int PredictionAwayScore { get; set; }
        public bool? isCorrect { get; set; }
        public int Points { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public User User { get; set; } = null!;
        public Match Match { get; set; } = null!;

  
    
    }
}
