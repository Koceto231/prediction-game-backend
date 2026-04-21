namespace BPFL.API.Modules.Bettings.Application.DTOs
{
    public class PlaceBetRequestDTO
    {
        public int MatchId { get; set; }

        public string MarketCode { get; set; } = null!;

        public string SelectionCode { get; set; } = null!;

        public int? PlayerId { get; set; }

        public decimal? LineValue { get; set; }

        public decimal Stake { get; set; }
    }
}
