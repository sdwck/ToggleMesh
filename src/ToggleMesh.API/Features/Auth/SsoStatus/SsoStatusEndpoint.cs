using FastEndpoints;

namespace ToggleMesh.API.Features.Auth.SsoStatus;

public class SsoStatusEndpoint : EndpointWithoutRequest<SsoStatusResponse>
{
    private readonly IConfiguration _configuration;

    public SsoStatusEndpoint(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public override void Configure()
    {
        Get("/auth/sso/status");
        Version(1);
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var clientId = _configuration["OIDC:ClientId"];
        var enabled = !string.IsNullOrEmpty(clientId);

        await Send.OkAsync(new SsoStatusResponse { Enabled = enabled }, cancellation: ct);
    }
}