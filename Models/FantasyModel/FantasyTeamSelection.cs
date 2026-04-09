namespace BPFL.API.Models.FantasyModel
{
    public class FantasyTeamSelection
    {
        public int Id { get; set; }

        public int FantasyTeamId { get; set; }

        public FantasyTeam FantasyTeam { get; set; } = null!;

        public int FantasyPlayerId { get; set; }

        public FantasyPlayer FantasyPlayer { get; set; } = null!;

        public int FantasyGameweekId { get; set; }

        public FantasyGameweek FantasyGameweek { get; set; } = null!;

        public bool IsCaptain { get; set; }  = false;

        public DateTime CreatedAt { get; set; }

        public DateTime LastUpdatedAt { get; set; }


    }
}
