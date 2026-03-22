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

        public List<Player> Players { get; set; } = new List<Player>();
        
    }
}
