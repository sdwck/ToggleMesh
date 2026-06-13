namespace ToggleMesh.API.Features.Auth.Models;

public class PersonalAccessToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;
    
    public string Name { get; set; } = string.Empty;
    public string TokenHash { get; set; } = string.Empty;
    public string TokenPreview { get; set; } = string.Empty;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
}