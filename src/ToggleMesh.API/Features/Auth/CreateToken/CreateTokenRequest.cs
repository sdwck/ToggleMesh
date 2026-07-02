namespace ToggleMesh.API.Features.Auth.CreateToken;

public class CreateTokenRequest
{
    public string Name { get; set; } = string.Empty;
    public int? ExpiresInDays { get; set; }
}