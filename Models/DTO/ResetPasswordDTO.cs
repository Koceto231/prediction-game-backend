namespace BPFL.API.Models.DTO
{
    public class ResetPasswordDTO
    {
        public string Token { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }
}
