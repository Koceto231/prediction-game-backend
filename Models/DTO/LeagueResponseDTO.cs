namespace BPFL.API.Models.DTO
{

    public class LeagueResponseDTO
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string InviteCode { get; set; } = null!;
        public string OwnerUsername { get; set; } = null!;
        public int MemberCount { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class LeagueLeaderboardEntryDTO
    {
        public int Rank { get; set; }
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public int TotalPoints { get; set; }
        public int CorrectResults { get; set; }  
        public int TotalPredictions { get; set; }
    }
}
