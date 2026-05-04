namespace BPFL.API.Features.Leaderboard
{
    public class LeaderboardResponseDTO
    {
        public int UserId { get; set; }

        public string Username { get; set; } = null!;

        public int TotalPoints { get; set; }

        public int CorrectResults { get; set; }
    }
}
