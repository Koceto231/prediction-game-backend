namespace BPFL.API.Modules.AI.Application.DTOs
{
    public class ShotsAgentResponse
    {
        public decimal OverShotsLean { get; set; }
        public decimal UnderShotsLean { get; set; }

        public decimal HomeMoreShotsLean { get; set; }
        public decimal AwayMoreShotsLean { get; set; }

        public decimal Confidence { get; set; }

        public string Summary { get; set; } = string.Empty;
    }
}
