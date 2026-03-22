namespace BPFL.API.Models.ExternalDto
{
    public class CompetitionResponseDto
    {
      public int Count { get; set; }

        public List<CompetiotionDTO> Competitions { get; set; } = null!;
    }
}
