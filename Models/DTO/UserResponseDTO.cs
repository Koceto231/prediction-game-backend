namespace BPFL.API.Models.DTO
{
    public class UserResponseDTO
    {
        public int Id { get; set; }
        public string Username { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string Role { get; set; } = null!;
        public decimal Balance { get; set; }
    }
}
