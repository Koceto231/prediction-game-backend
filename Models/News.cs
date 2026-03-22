using System.ComponentModel.DataAnnotations;

namespace BPFL.API.Models
{
    public class News
    {

        public int Id { get; set; }

        [Required]
        public string Title { get; set; } = null!;

        [Required]
        public string Content { get; set; } = null!;

        public DateTime PublishedAt { get; set; } = DateTime.UtcNow;
    }
}
