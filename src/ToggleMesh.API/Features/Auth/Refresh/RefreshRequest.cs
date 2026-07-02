namespace ToggleMesh.API.Features.Auth.Refresh;

public class RefreshRequest
{
    public string Token { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
}