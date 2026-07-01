namespace ToggleMesh.API.Features.Auth.Endpoints.CreateToken;

public class CreateTokenRequest
{
    public string Name { get; set; } = string.Empty;
    public int? ExpiresInDays { get; set; }
}