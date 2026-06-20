using System.ComponentModel.DataAnnotations.Schema;
using ToggleMesh.API.Persistence.Abstractions;

namespace ToggleMesh.API.Features.Auth.Models;

public class RefreshToken : Entity
{
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