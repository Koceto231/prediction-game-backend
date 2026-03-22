namespace BPFL.API.Models.DTO
{
    public class MatchDto
    {
        public int Id { get; set; }
        public DateTime MatchDate { get; set; }
        public string Status { get; set; } = null!;
        public int? MatchDay { get; set; }

        public int HomeTeamId { get; set; }
        public string HomeTeamName { get; set; } = null!;
        public int AwayTeamId { get; set; }
        public string AwayTeamName { get; set; } = null!;

        public int? HomeScore { get; set; }
        public int? AwayScore { get; set; }
    }
}
