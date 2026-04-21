namespace BPFL.API.Modules.AI.Application.DTOs
{
    public class HeadToHeadResponse
    {
        public int HomeTeamId { get; set; }
        public int AwayTeamId { get; set; }

        public List<HeadToHeadMatchResponse> Matches { get; set; } = new();
    }
}
