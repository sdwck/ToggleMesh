using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Endpoints;

namespace ToggleMesh.API.Features.Auth.Endpoints.DeleteToken;

public class DeleteTokenEndpoint : ToggleEndpointWithoutRequest
{
    private readonly AppDbContext _db;

    public DeleteTokenEndpoint(AppDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Delete("/user/tokens/{id:guid}");
        Version(1);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");
        var token = await _db.PersonalAccessTokens
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == UserId, ct);

        if (token != null)
        {
            _db.PersonalAccessTokens.Remove(token);
            await _db.SaveChangesAsync(ct);
        }

        await Send.NoContentAsync(ct);
    }
}