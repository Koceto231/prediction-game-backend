using BPFL.API.Modules.Bettings.Domain.Enums;

namespace BPFL.API.Modules.Bettings.Application.DTOs
{
    public class PlaceBetResponseDTO
    {
        public int BetId { get; set; }

        public decimal Stake { get; set; }

        public decimal Odds { get; set; }

        public decimal PotentialReturn { get; set; }

        public BetStatus Status { get; set; }

        public decimal RemainingBalance { get; set; }
    }
}
