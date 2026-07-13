using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Extensions;
using ToggleMesh.API.Features.Flags.Commands;
using ToggleMesh.API.Features.Flags.Domain;
using ToggleMesh.API.Features.Segments.Domain;
using ToggleMesh.API.Infrastructure.Data;
using AuthModels = ToggleMesh.API.Infrastructure.Security.Authorization.Models;


namespace ToggleMesh.API.Features.Segments.Update;

public class UpdateSegmentEndpoint : Endpoint<UpdateSegmentRequest, SegmentDto>
{
    private readonly AppDbContext _db;
    public UpdateSegmentEndpoint(AppDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Put("/projects/{projectId}/environments/{environmentId}/segments/{segmentId}");
        this.RequirePermission(AuthModels.Permissions.EnvironmentsEdit);
    }

    public override async Task HandleAsync(UpdateSegmentRequest req, CancellationToken ct)
    {
        var envId = Route<Guid>("environmentId");
        var segmentId = Route<Guid>("segmentId");

        var segment = await _db.Segments
            .Include(x => x.Rules)
            .FirstOrDefaultAsync(x => x.Id == segmentId && x.EnvironmentId == envId, ct);

        if (segment == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        segment.Name = req.Name;
        segment.Description = req.Description;

        _db.SegmentRules.RemoveRange(segment.Rules);
        
        segment.Rules = req.Rules.Select(r => new SegmentRule
        {
            SegmentId = segment.Id,
            GroupId = r.GroupId,
            Attribute = r.Attribute,
            Operator = r.Operator,
            Value = r.Value
        }).ToList();

        await _db.SaveChangesAsync(ct);

        var affectedStates = await _db.FlagEnvironmentStates
            .Include(x => x.FeatureFlag)
                .ThenInclude(x => x.Variations)
            .Include(x => x.Rules)
            .Where(x => x.Rules.Any(r => r.Operator == "InSegment" && r.Value == segmentId.ToString()))
            .AsSplitQuery()
            .ToListAsync(ct);

        foreach (var state in affectedStates)
        {
            var response = state.ToDto();
            await new NotifyFlagUpdatedCommand(state.EnvironmentId, state.FeatureFlag.Key, response, state.ToSdkDto()).ExecuteAsync(ct);
        }

        var dto = new SegmentDto(
            segment.Id,
            segment.EnvironmentId,
            segment.Name,
            segment.Description,
            segment.Rules.Select(r => new RuleInput(r.GroupId, r.Attribute, r.Operator, r.Value)),
            segment.CreatedAt
        );

        await Send.OkAsync(dto, ct);
    }
}

