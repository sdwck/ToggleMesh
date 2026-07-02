using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Extensions;
using ToggleMesh.API.Infrastructure.Data;
using AuthModels = ToggleMesh.API.Infrastructure.Security.Authorization.Models;


namespace ToggleMesh.API.Features.Segments.Delete;

public class DeleteSegmentEndpoint : EndpointWithoutRequest
{
    private readonly AppDbContext _db;

    public DeleteSegmentEndpoint(AppDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Delete("/projects/{projectId}/environments/{environmentId}/segments/{segmentId}");
        this.RequirePermission(AuthModels.Permissions.EnvironmentsEdit);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var envId = Route<Guid>("environmentId");
        var segmentId = Route<Guid>("segmentId");

        var stringId = segmentId.ToString();
        var isUsed = await _db.FlagRules
            .AnyAsync(r => r.Operator == "InSegment" && r.Value == stringId, ct);

        if (isUsed)
        {
            AddError("Cannot delete segment because it is currently used in one or more feature flag rules.");
            await Send.ErrorsAsync(400, ct);
            return;
        }

        var segment = await _db.Segments
            .FirstOrDefaultAsync(x => x.Id == segmentId && x.EnvironmentId == envId, ct);

        if (segment == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        _db.Segments.Remove(segment);
        await _db.SaveChangesAsync(ct);

        await Send.NoContentAsync(ct);
    }
}
