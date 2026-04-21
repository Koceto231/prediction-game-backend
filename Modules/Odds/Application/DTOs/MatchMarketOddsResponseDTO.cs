namespace BPFL.API.Modules.Odds.Application.DTOs
{
    public class MatchMarketOddsResponseDTO
    {
        public string MarketCode { get; set; } = null!;

        public string SelectionCode { get; set; } = null!;

        public int? PlayerId { get; set; }

        public decimal? LineValue { get; set; }

        public decimal Odds { get; set; }

        public DateTime UpdatedAt { get; set; }
    }
}
