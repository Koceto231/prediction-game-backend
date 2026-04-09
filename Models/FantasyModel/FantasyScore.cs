namespace BPFL.API.Models.FantasyModel
{
    public class FantasyScore
    {
        public int Id { get; set; }

        public int FantasyTeamId { get; set; }

        public FantasyTeam FantasyTeam { get; set; } = null!;

        public int FantasyGameweekId { get; set; }

        public FantasyGameweek FantasyGameweek { get; set;} = null!;

        public bool IsFinalized { get; set; }

        public int WeeklyPoints { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime LastUpdatedAt { get; set; }

    }
}
