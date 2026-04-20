using BPFL.API.Models;
using static BPFL.API.Models.Predictionenums;

namespace BPFL.API.Models.DTO
{
    public class PlaceBetDTO
    {
        public int MatchId { get; set; }
        public MatchWinner Pick { get; set; }
        public decimal Amount { get; set; }
    }

    public class BetResponseDTO
    {
        public int Id { get; set; }
        public int MatchId { get; set; }
        public string HomeTeam { get; set; } = null!;
        public string AwayTeam { get; set; } = null!;
        public DateTime MatchDate { get; set; }
        public string Pick { get; set; } = null!;
        public decimal Amount { get; set; }
        public decimal OddsAtBetTime { get; set; }
        public decimal PotentialPayout { get; set; }
        public BetStatus Status { get; set; }
        public decimal? ActualPayout { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
