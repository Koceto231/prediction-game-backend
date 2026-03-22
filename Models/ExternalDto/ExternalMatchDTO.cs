namespace BPFL.API.Models.ExternalDto
{
    public class ExternalMatchDTO
    {
        public int Id { get; set; }

        public DateTime UtcDate { get; set; }

        public string Status { get; set; } = null!;

        public int MatchDay { get; set; }

        public ExternalTeamRefDTO HomeTeam { get; set; } = null!;

        public ExternalTeamRefDTO AwayTeam { get; set; } = null!;

        public ExternalScoreDTO Score { get; set; } = null!;


    }
}
