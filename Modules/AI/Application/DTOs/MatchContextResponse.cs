namespace BPFL.API.Modules.AI.Application.DTOs
{
    public class MatchContextResponse
    {
        public int MatchId { get; set; }
        public DateTime MatchDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public int? MatchDay { get; set; }

        public int HomeTeamId { get; set; }
        public string HomeTeamName { get; set; } = string.Empty;

        public int AwayTeamId { get; set; }
        public string AwayTeamName { get; set; } = string.Empty;

        public int? HomeScore { get; set; }
        public int? AwayScore { get; set; }
    }
}
