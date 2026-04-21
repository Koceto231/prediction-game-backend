using BPFL.API.Models;

namespace BPFL.API.Modules.Odds.Domain.Entities
{
    public class MatchMarketOdds
    {
        public int Id { get; set; }

        public int MatchId { get; set; }

        public string MarketCode { get; set; } = null!;

        public string SelectionCode { get; set; } = null!;

        public int? PlayerId { get; set; }

        public decimal? LineValue { get; set; }

        public decimal Odds { get; set; }

        public DateTime UpdatedAt { get; set; }

        public Match Match { get; set; } = null!;
    }
}
