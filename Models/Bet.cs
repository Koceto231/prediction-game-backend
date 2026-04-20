using static BPFL.API.Models.Predictionenums;

namespace BPFL.API.Models
{
    public enum BetStatus { Pending, Won, Lost, Void }

    public enum BetType { Winner = 1, ExactScore = 2, BTTS = 3, OverUnder = 4 }

    public class Bet
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public User User { get; set; } = null!;
        public int MatchId { get; set; }
        public Match Match { get; set; } = null!;

        public BetType BetType { get; set; } = BetType.Winner;

        // Winner bet
        public MatchWinner? Pick { get; set; }

        // Exact score bet
        public int? ScoreHome { get; set; }
        public int? ScoreAway { get; set; }

        // BTTS bet
        public bool? BTTSPick { get; set; }

        // Over/Under bet
        public OverUnderLine? OULine { get; set; }
        public OverUnderPick? OUPick { get; set; }

        public decimal Amount { get; set; }
        public decimal OddsAtBetTime { get; set; }
        public decimal PotentialPayout { get; set; }
        public BetStatus Status { get; set; } = BetStatus.Pending;
        public decimal? ActualPayout { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
