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

       
        public string? Password { get; set; }

        public string? GoogleId { get; set; }

        public string Role { get; set; } = "User";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public List<Prediction> Predictions { get; set; } = new List<Prediction>();
        public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();

        public bool IsEmailVerified { get; set; } = false;

        public string? EmailVerificationToken { get; set; }
        public DateTime? EmailVerificationTokenExpires { get; set; }

        public string? PasswordResetToken { get; set; }
        public DateTime? PasswordResetTokenExpires { get; set; }

    }
}
