namespace BPFL.API.Modules.AI.Application.DTOs
{
    public class CornersAgentResponse
    {
        public decimal OverCornersLean { get; set; }
        public decimal UnderCornersLean { get; set; }

        public decimal HomeMoreCornersLean { get; set; }
        public decimal AwayMoreCornersLean { get; set; }

        public decimal Confidence { get; set; }

        public string Summary { get; set; } = string.Empty;
    }
}
