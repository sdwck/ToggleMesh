using System.Security.Cryptography;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Endpoints;
using ToggleMesh.API.Infrastructure.Security;
using ToggleMesh.API.Infrastructure.Security.Authorization.Models;

namespace ToggleMesh.API.Features.Auth.CreateToken;

public class CreateTokenEndpoint : ToggleEndpoint<CreateTokenRequest, CreateTokenResponse>
{
    private readonly AppDbContext _db;
    private readonly TimeProvider _timeProvider;

    public CreateTokenEndpoint(AppDbContext db, TimeProvider timeProvider)
    {
        _db = db;
        _timeProvider = timeProvider;
    }

    public override void Configure()
    {
        Post("/user/tokens");
        Version(1);
    }

    public override async Task HandleAsync(CreateTokenRequest req, CancellationToken ct)
    {
        var rawSecret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace("+", "").Replace("/", "").Replace("=", "");

        var plainToken = $"tmp_{rawSecret}";
        var tokenHash = ApiKeyHasher.Hash(plainToken);
        var tokenPreview = $"tmp_{rawSecret[..4]}***{rawSecret[^4..]}";

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var expiresAt = req.ExpiresInDays.HasValue 
            ? now.AddDays(req.ExpiresInDays.Value) 
            : (DateTime?)null;

        var pat = new PersonalAccessToken
        {
            Id = Guid.CreateVersion7(),
            UserId = UserId,
            Name = req.Name.Trim(),
            TokenHash = tokenHash,
            TokenPreview = tokenPreview,
            CreatedAt = now,
            ExpiresAt = expiresAt
        };

        _db.PersonalAccessTokens.Add(pat);
        await _db.SaveChangesAsync(ct);

        await Send.OkAsync(new CreateTokenResponse
        {
            Id = pat.Id,
            Name = pat.Name,
            PlainToken = plainToken,
            CreatedAt = pat.CreatedAt,
            ExpiresAt = pat.ExpiresAt
        }, ct);
    }
}