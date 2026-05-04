namespace BPFL.API.Features.Auth
{
    public class AuthTokenDTO
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
    }
}
