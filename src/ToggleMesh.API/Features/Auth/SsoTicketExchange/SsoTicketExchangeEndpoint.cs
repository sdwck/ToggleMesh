using System.Text.Json;
using StackExchange.Redis;
using ToggleMesh.API.Features.Auth.Login;
using ToggleMesh.API.Infrastructure.Endpoints;

namespace ToggleMesh.API.Features.Auth.SsoTicketExchange;

public class SsoTicketExchangeEndpoint : ToggleEndpoint<SsoTicketExchangeRequest, LoginResponse>
{
    private readonly IConnectionMultiplexer _redisConnection;

    public SsoTicketExchangeEndpoint(IConnectionMultiplexer redisConnection)
    {
        _redisConnection = redisConnection;
    }

    public override void Configure()
    {
        Post("/auth/sso/exchange");
        Version(1);
        AllowAnonymous();
    }

    public override async Task HandleAsync(SsoTicketExchangeRequest req, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(req.Ticket))
            ThrowError("Ticket is required");

        var redis = _redisConnection.GetDatabase();
        var key = $"sso:ticket:{req.Ticket}";
        var data = await redis.StringGetDeleteAsync(key);

        if (!data.HasValue)
            ThrowError("Invalid or expired SSO ticket");

        var tokens = JsonSerializer.Deserialize<SsoTicketData>((string)data!);
        if (tokens == null)
            ThrowError("Invalid SSO ticket data");

        if (tokens.RequiresTwoFactor)
        {
            await Send.OkAsync(new LoginResponse
            {
                RequiresTwoFactor = true,
                TwoFactorToken = tokens.TwoFactorToken
            }, cancellation: ct);
            return;
        }

        await Send.OkAsync(new LoginResponse
        {
            Token = tokens.AccessToken,
            RefreshToken = tokens.RefreshToken
        }, cancellation: ct);
    }
}
