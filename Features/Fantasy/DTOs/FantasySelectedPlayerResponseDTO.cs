namespace BPFL.API.Features.Fantasy
{
    public class FantasySelectedPlayerResponseDTO
    {
        public int FantasyPlayerId { get; set; }

        public string Name { get; set; } = null!;

        public string Position { get; set; } = null!;

        public string TeamName { get; set; } = null!;

        public decimal Price { get; set; }

        public bool IsCaptain { get; set; }

        public int Points { get; set; }
    }
}
