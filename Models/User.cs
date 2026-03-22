using System.ComponentModel.DataAnnotations;

namespace BPFL.API.Models
{
    public class User
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string Username { get; set; } = null!;

        [Required]
        public string Email { get; set; } = null!;

        [Required]
        public string Password { get; set; } = null!;

        public string Role { get; set; } = "User";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public List<Prediction> Predictions { get; set; } = new List<Prediction>();
        public List<RefreshToken> RefreshToken { get; set; } = new List<RefreshToken>();

    }
}
