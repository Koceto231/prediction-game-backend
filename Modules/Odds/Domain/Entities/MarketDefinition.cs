namespace BPFL.API.Modules.Odds.Domain.Entities
{
    public class MarketDefinition
    {
        public int Id { get; set; }

        public string Code { get; set; } = null!; // =>  MATCH_WINNER, BTTS, TOTAL_GOALS
        public string Name { get; set; } = null!;
        public string Category { get; set; } = null!;

        public bool RequiresPlayer { get; set; }

        public bool RequiresLine { get; set; }

        public bool IsActive { get; set; }
    }
}
