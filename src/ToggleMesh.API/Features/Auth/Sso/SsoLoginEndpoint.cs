using FastEndpoints;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;

namespace ToggleMesh.API.Features.Auth.Sso;

public class SsoLoginEndpoint : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/auth/sso/login");
        Version(1);
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var properties = new AuthenticationProperties
        {
            RedirectUri = "/api/v1/auth/sso/callback-handler"
        };

        await HttpContext.ChallengeAsync(OpenIdConnectDefaults.AuthenticationScheme, properties);
    }
}
