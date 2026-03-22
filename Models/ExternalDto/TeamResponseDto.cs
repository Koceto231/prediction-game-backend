namespace BPFL.API.Models.ExternalDto
{
    public class TeamResponseDto
    {
        public int Count { get; set; }

        public List<ExternalTeamDTO> Teams { get; set; } = null!;
    }
}
