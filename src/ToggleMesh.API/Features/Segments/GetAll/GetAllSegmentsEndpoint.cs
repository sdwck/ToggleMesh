using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Extensions;
using ToggleMesh.API.Features.Flags.Domain;
using ToggleMesh.API.Features.Segments.Domain;
using ToggleMesh.API.Infrastructure.Data;
using AuthModels = ToggleMesh.API.Infrastructure.Security.Authorization.Models;


namespace ToggleMesh.API.Features.Segments.GetAll;

public class GetAllSegmentsEndpoint : EndpointWithoutRequest<IEnumerable<SegmentDto>>
{
    private readonly AppDbContext _db;

    public GetAllSegmentsEndpoint(AppDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Get("/projects/{projectId}/environments/{environmentId}/segments");
        this.RequirePermission(AuthModels.Permissions.EnvironmentsView);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var envId = Route<Guid>("environmentId");

        var segments = await _db.Segments
            .Include(x => x.Rules)
            .Where(x => x.EnvironmentId == envId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);

        var response = segments.Select(s => new SegmentDto(
            s.Id,
            s.EnvironmentId,
            s.Name,
            s.Description,
            s.Rules.Select(r => new RuleInput(r.GroupId, r.Attribute, r.Operator, r.Value)),
            s.CreatedAt
        ));

        await Send.OkAsync(response, ct);
    }
}

