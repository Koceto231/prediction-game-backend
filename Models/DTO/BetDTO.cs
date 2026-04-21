using BPFL.API.Models;
using static BPFL.API.Models.Predictionenums;

namespace BPFL.API.Models.DTO
{
    public class PlaceBetDTO
    {
        public int MatchId { get; set; }
        public BetType BetType { get; set; }
        public decimal Amount { get; set; }

        // Winner
        public MatchWinner? Pick { get; set; }

        // Exact score
        public int? ScoreHome { get; set; }
        public int? ScoreAway { get; set; }

        // BTTS
        public bool? BTTSPick { get; set; }

        // Over/Under
        public OverUnderLine? OULine { get; set; }
        public OverUnderPick? OUPick { get; set; }
    }

    public class BetResponseDTO
    {
        public int Id { get; set; }
        public int MatchId { get; set; }
        public string HomeTeam { get; set; } = null!;
        public string AwayTeam { get; set; } = null!;
        public DateTime MatchDate { get; set; }
        public BetType BetType { get; set; }
        public string BetDescription { get; set; } = null!;
        public decimal Amount { get; set; }
        public decimal OddsAtBetTime { get; set; }
        public decimal PotentialPayout { get; set; }
        public BetStatus Status { get; set; }
        public decimal? ActualPayout { get; set; }
        public DateTime CreatedAt { get; set; }
        /// <summary>Maximum prediction points this bet type can earn.</summary>
        public int MaxPoints { get; set; }
    }

    public class BetOddsDTO
    {
        public decimal Odds { get; set; }
        public string Description { get; set; } = null!;
    }
}
