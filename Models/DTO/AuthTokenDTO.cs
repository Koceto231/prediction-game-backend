namespace BPFL.API.Models.DTO
{
    public class AuthTokenDTO
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
    }
}
