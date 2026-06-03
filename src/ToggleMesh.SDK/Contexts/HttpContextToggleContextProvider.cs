using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace ToggleMesh.SDK.Contexts;

public class HttpContextToggleContextProvider : IToggleMeshContextProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextToggleContextProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public bool TryGetValue(string key, out string? value)
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user == null || !user.Identity?.IsAuthenticated == true)
        {
            value = null;
            return false;
        }
        
        if (string.Equals(key, "UserId", StringComparison.OrdinalIgnoreCase))
        {
            value = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return value != null;
        }
        
        if (string.Equals(key, "Email", StringComparison.OrdinalIgnoreCase))
        {
            value = user.FindFirst(ClaimTypes.Email)?.Value;
            return value != null;
        }
        
        if (string.Equals(key, "Roles", StringComparison.OrdinalIgnoreCase))
        {
            var roles = user.FindAll(ClaimTypes.Role).Select(r => r.Value);
            value = string.Join(",", roles);
            return !string.IsNullOrEmpty(value);
        }

        value = null;
        return false;
    }
}