namespace ToggleMesh.API.Features.Auth.CreateToken;

public class CreateTokenResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string PlainToken { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
}