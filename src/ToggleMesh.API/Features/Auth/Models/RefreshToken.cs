using System.ComponentModel.DataAnnotations.Schema;

namespace ToggleMesh.API.Features.Auth.Models;
// TODO: Add a worker to clean up expired
public class RefreshToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Token { get; set; } = string.Empty;
    public DateTime Expires { get; set; }
    public DateTime Created { get; set; }
    public DateTime? Revoked { get; set; }

    [NotMapped]
    public bool IsExpired => DateTime.UtcNow >= Expires;
    [NotMapped]
    public bool IsActive => Revoked == null && !IsExpired;

    public ApplicationUser User { get; set; } = null!;
}