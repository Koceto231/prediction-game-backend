using System.ComponentModel.DataAnnotations;

namespace BPFL.API.Models
{
    public class Player
    {
        public int Id { get; set; }

        [Required]
        public string FullName { get; set; } = null!;

        [Required]
        public string Position { get; set; } = null!;

        public int TeamId { get; set; }

        public Team Team { get; set; } = null!;

    }
}
