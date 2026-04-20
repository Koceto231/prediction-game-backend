using static BPFL.API.Models.Predictionenums;

namespace BPFL.API.Models
{
    public enum BetStatus { Pending, Won, Lost, Void }

    public class Bet
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public User User { get; set; } = null!;
        public int MatchId { get; set; }
        public Match Match { get; set; } = null!;
        public MatchWinner Pick { get; set; }
        public decimal Amount { get; set; }
        public decimal OddsAtBetTime { get; set; }
        public decimal PotentialPayout { get; set; }
        public BetStatus Status { get; set; } = BetStatus.Pending;
        public decimal? ActualPayout { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
