using FastEndpoints;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

namespace ToggleMesh.API.Features.Auth.Endpoints.SsoLogin;

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

        await Send.ResultAsync(
            Results.Challenge(
                properties, 
                [OpenIdConnectDefaults.AuthenticationScheme]));
    }
}
