namespace BPFL.API.Models.FantasyDTO
{
    public class FantasyLeaderboardRowDTO
    {
        public int Rank { get; set; }

        public int UserId { get; set; }

        public string Username { get; set; } = null!;

        public string FantasyTeamName { get; set; } = null!;

        public int WeeklyPoints { get; set; }
    }
}
