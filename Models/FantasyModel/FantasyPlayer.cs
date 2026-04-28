namespace BPFL.API.Models.FantasyModel
{
    public class FantasyPlayer
    {
        public int Id { get; set; } 

        public int ExternalPlayerId { get; set; }

        public string Name { get; set; } = null!;

        public enum FantasyPosition 
        { 
           GK,
           DEF,
           MID,
           FWD
        } 

        public FantasyPosition Position { get; set; } 


        public int TeamId { get; set; }

        public Team Team { get; set; } = null!;

        public decimal Price { get; set; }

        public bool IsActive { get; set; } = true;
        public string? PhotoUrl { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastUpdatedAt { get; set; }

    }
}
