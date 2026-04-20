namespace BPFL.API.Models.FantasyDTO
{
    public class AddFantasyPlayerDTO
    {
        public string Name { get; set; } = null!;
        /// <summary>GK | DEF | MID | FWD</summary>
        public string Position { get; set; } = null!;
        public int TeamId { get; set; }
        public decimal Price { get; set; }
        public int ExternalPlayerId { get; set; }
    }
}
