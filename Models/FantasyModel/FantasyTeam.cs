namespace BPFL.API.Models.FantasyModel
{
    public class FantasyTeam
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        public User User { get; set; } = null!;

        public string TeamName { get; set; } = null!;

        public decimal Budget { get; set; }

        public decimal RemainingBudget { get; set; } 

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

    }
}
