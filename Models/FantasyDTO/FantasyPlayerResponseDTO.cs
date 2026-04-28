namespace BPFL.API.Models.FantasyDTO
{
    public class FantasyPlayerResponseDTO
    {
        public int Id { get; set; }

        public string Name { get; set; } = null!;

        public string Position { get; set; } = null!;

        public int TeamId { get; set; }

        public string TeamName { get; set; } = null!; 

        public decimal Price { get; set; }

        public string? PhotoUrl { get; set; }
    }
}
