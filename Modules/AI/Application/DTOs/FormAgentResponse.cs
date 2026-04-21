namespace BPFL.API.Modules.AI.Application.DTOs
{
    public class FormAgentResponse
    {
        public decimal HomeWinLean { get; set; }
        public decimal DrawLean { get; set; }
        public decimal AwayWinLean { get; set; }

        public decimal Confidence { get; set; }

        public string Summary { get; set; } = string.Empty;
    }
}

