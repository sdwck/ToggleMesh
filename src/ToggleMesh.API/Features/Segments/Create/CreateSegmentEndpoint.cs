using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Extensions;
using ToggleMesh.API.Features.Flags.Domain;
using ToggleMesh.API.Features.Segments.Domain;
using ToggleMesh.API.Infrastructure.Data;
using AuthModels = ToggleMesh.API.Infrastructure.Security.Authorization.Models;


namespace ToggleMesh.API.Features.Segments.Create;

public class CreateSegmentEndpoint : Endpoint<CreateSegmentRequest, SegmentDto>
{
    private readonly AppDbContext _db;

    public CreateSegmentEndpoint(AppDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Post("/projects/{projectId}/environments/{environmentId}/segments");
        this.RequirePermission(AuthModels.Permissions.EnvironmentsEdit);
    }

    public override async Task HandleAsync(CreateSegmentRequest req, CancellationToken ct)
    {
        var envId = Route<Guid>("environmentId");

        var environment = await _db.Environments
            .Include(x => x.Project)
            .FirstOrDefaultAsync(x => x.Id == envId, ct);

        if (environment == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var segment = new Segment
        {
            EnvironmentId = envId,
            Name = req.Name,
            Description = req.Description,
            Rules = req.Rules.Select(r => new SegmentRule
            {
                GroupId = r.GroupId,
                Attribute = r.Attribute,
                Operator = r.Operator,
                Value = r.Value
            }).ToList()
        };

        _db.Segments.Add(segment);
        await _db.SaveChangesAsync(ct);

        var dto = new SegmentDto(
            segment.Id,
            segment.EnvironmentId,
            segment.Name,
            segment.Description,
            segment.Rules.Select(r => new RuleInput(r.GroupId, r.Attribute, r.Operator, r.Value)),
            segment.CreatedAt
        );

        await Send.CreatedAtAsync($"/projects/{environment.ProjectId}/environments/{envId}/segments/{segment.Id}", null, dto, cancellation: ct);
    }
}

