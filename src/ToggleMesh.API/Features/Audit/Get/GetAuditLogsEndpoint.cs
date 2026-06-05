using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Infrastructure;
using ToggleMesh.API.Persistence;

namespace ToggleMesh.API.Features.Audit.Get;

public class GetAuditLogsRequest
{
    public Guid? ProjectId { get; set; }
    public Guid? EnvironmentId { get; set; }
}

public class GetAuditLogsResponse
{
    public List<AuditLogDto> Logs { get; set; } = [];
}

public record AuditLogDto(
    Guid Id,
    Guid? EnvironmentId,
    string EntityName,
    string EntityId,
    string Action,
    string OldValues,
    string NewValues,
    string PerformedBy,
    DateTime Timestamp);

public class GetAuditLogsEndpoint : ToggleEndpoint<GetAuditLogsRequest, GetAuditLogsResponse>
{
    private readonly AppDbContext _db;

    public GetAuditLogsEndpoint(AppDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Get("/audit-logs");
        Version(1);
        Policies($"Permission:{Auth.Models.Permissions.ProjectsView}"); // Users who can view projects can view audit logs
    }

    public override async Task HandleAsync(GetAuditLogsRequest req, CancellationToken ct)
    {
        var query = _db.AuditLogs.AsNoTracking();

        if (req.EnvironmentId.HasValue)
        {
            query = query.Where(x => x.EnvironmentId == req.EnvironmentId);
        }
        else if (req.ProjectId.HasValue)
        {
            var envIds = await _db.Environments
                .Where(e => e.ProjectId == req.ProjectId)
                .Select(e => e.Id)
                .ToListAsync(ct);
            
            query = query.Where(x => x.EnvironmentId != null && envIds.Contains(x.EnvironmentId.Value));
        }

        var logs = await query
            .OrderByDescending(x => x.Timestamp)
            .Take(100)
            .Select(x => new AuditLogDto(
                x.Id,
                x.EnvironmentId,
                x.EntityName,
                x.EntityId,
                x.Action,
                x.OldValues,
                x.NewValues,
                x.PerformedBy,
                x.Timestamp))
            .ToListAsync(ct);

        await Send.OkAsync(new GetAuditLogsResponse { Logs = logs }, ct);
    }
}
