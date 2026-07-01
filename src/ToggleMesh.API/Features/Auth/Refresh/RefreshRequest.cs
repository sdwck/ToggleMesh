namespace ToggleMesh.API.Features.Auth.Endpoints.Refresh;

public class RefreshRequest
{
    public string Token { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
}