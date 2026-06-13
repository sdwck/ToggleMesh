using System.Security.Cryptography;
using ToggleMesh.API.Features.Auth.Models;
using ToggleMesh.API.Infrastructure;
using ToggleMesh.API.Infrastructure.Security;
using ToggleMesh.API.Persistence;

namespace ToggleMesh.API.Features.Auth.Endpoints.CreateToken;

public class CreateTokenEndpoint : ToggleEndpoint<CreateTokenRequest, CreateTokenResponse>
{
    private readonly AppDbContext _db;

    public CreateTokenEndpoint(AppDbContext db)
    {
        _db = db;
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

        var expiresAt = req.ExpiresInDays.HasValue 
            ? DateTime.UtcNow.AddDays(req.ExpiresInDays.Value) 
            : (DateTime?)null;

        var pat = new PersonalAccessToken
        {
            Id = Guid.CreateVersion7(),
            UserId = UserId,
            Name = req.Name.Trim(),
            TokenHash = tokenHash,
            TokenPreview = tokenPreview,
            CreatedAt = DateTime.UtcNow,
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