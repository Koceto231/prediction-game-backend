namespace BPFL.API.Modules.AI.Application.DTOs
{
    public class TrendsAgentResponse
    {
        public decimal BTTSYesLean { get; set; }
        public decimal BTTSNoLean { get; set; }

        public decimal Over25Lean { get; set; }
        public decimal Under25Lean { get; set; }

        public decimal Confidence { get; set; }

        public string Summary { get; set; } = string.Empty;
    }
}
