using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace ToggleMesh.API.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(JwtRegisteredClaimNames.Sub) 
                    ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var id)
            ? id
            : throw new InvalidOperationException("User ID claim is missing or invalid.");
    }

    public static bool TryGetUserId(this ClaimsPrincipal principal, out Guid userId)
    {
        var value = principal.FindFirstValue(JwtRegisteredClaimNames.Sub) 
                    ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out userId);
    }
}