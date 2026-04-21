using BPFL.API.Models;
using BPFL.API.Modules.Bettings.Domain.Enums;

namespace BPFL.API.Modules.Bettings.Domain.Entities
{
    public class Bet
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        public int MatchId { get; set; }

        public string MarketCode { get; set; } = null!;

        public string SelectionCode { get; set; } = null!;

        public int? PlayerId { get; set; }

        public decimal? LineValue { get; set; }

        public decimal Odds { get; set; }

        public decimal Stake { get; set; }

        public decimal PotentialReturn { get; set; }

        public BetStatus Status { get; set; } = BetStatus.Pending;

        public decimal? SettledReturn { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? SettledAt { get; set; }

        public User User { get; set; } = null!;

        public Match Match { get; set; } = null!;
    }
}
