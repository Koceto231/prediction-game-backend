namespace BPFL.API.Models.ExternalDto
{
    public class ExternalTeamDTO
    {
        public int Id { get; set; }

        public string Name { get; set; } = null!;

        public string ShortName { get; set; } = null!;

        public string Crest { get; set; } = null!;

        /// <summary>Squad players returned by the /competitions/{code}/teams endpoint.</summary>
        public List<ExternalSquadPlayerDTO> Squad { get; set; } = new();
    }
}
