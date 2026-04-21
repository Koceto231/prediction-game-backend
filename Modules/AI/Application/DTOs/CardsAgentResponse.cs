namespace BPFL.API.Modules.AI.Application.DTOs
{
    public class CardsAgentResponse
    {
        public decimal OverCardsLean { get; set; }
        public decimal UnderCardsLean { get; set; }

        public decimal HomeMoreCardsLean { get; set; }
        public decimal AwayMoreCardsLean { get; set; }

        public decimal Confidence { get; set; }

        public string Summary { get; set; } = string.Empty;
    }
}
