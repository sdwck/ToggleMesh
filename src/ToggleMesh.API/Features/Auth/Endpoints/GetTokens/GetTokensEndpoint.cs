using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Infrastructure;
using ToggleMesh.API.Persistence;

namespace ToggleMesh.API.Features.Auth.Endpoints.GetTokens;

public class GetTokensEndpoint : ToggleEndpointWithoutRequest<List<TokenDto>>
{
    private readonly AppDbContext _db;

    public GetTokensEndpoint(AppDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Get("/user/tokens");
        Version(1);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var tokens = await _db.PersonalAccessTokens
            .AsNoTracking()
            .Where(x => x.UserId == UserId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new TokenDto(x.Id, x.Name, x.TokenPreview, x.CreatedAt, x.ExpiresAt, x.LastUsedAt))
            .ToListAsync(ct);

        await Send.OkAsync(tokens, ct);
    }
}