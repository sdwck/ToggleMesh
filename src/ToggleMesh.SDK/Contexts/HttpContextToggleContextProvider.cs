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

    public Dictionary<string, string> GetContext()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user == null || !user.Identity?.IsAuthenticated == true)
            return new Dictionary<string, string>();

        return new Dictionary<string, string>
        {
            { "UserId", user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty },
            { "Email", user.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty },
            { "Roles", string.Join(",", user.FindAll(ClaimTypes.Role).Select(r => r.Value)) }
        };
    }
}