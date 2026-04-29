using System.ComponentModel.DataAnnotations;

namespace BPFL.API.Models
{
    public class Team
    {
        public int Id { get; set; }

        [Required]
        public int ExternalId { get; set; }

        [Required]
        public string Name { get; set; } = null!;

        public string? City { get; set; }

        public int? YearOfCreate { get; set; }

        /// <summary>Sportmonks league code this team currently plays in (PL, BGL, BL1, SA, PD).</summary>
        public string? LeagueCode { get; set; }
    }
}
