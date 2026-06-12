using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Infrastructure;
using ToggleMesh.API.Persistence;

namespace ToggleMesh.API.Features.Audit.Get;

public class GetAuditLogsRequest
{
    public Guid? ProjectId { get; set; }
    public Guid? EnvironmentId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class GetAuditLogsResponse
{
    public List<AuditLogDto> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public bool HasNextPage { get; set; }
    public bool HasPreviousPage { get; set; }
}

public record AuditLogDto(
    Guid Id,
    Guid? EnvironmentId,
    string EntityName,
    string EntityFriendlyName,
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
    }

    public override async Task HandleAsync(GetAuditLogsRequest req, CancellationToken ct)
    {
        if (!req.ProjectId.HasValue && !req.EnvironmentId.HasValue)
        {
            await Send.ForbiddenAsync(ct);
            return;
        }

        Guid projectIdToCheck = req.ProjectId ?? Guid.Empty;
        if (req.EnvironmentId.HasValue)
        {
            var env = await _db.Environments.FirstOrDefaultAsync(e => e.Id == req.EnvironmentId.Value, ct);
            if (env == null)
            {
                await Send.NotFoundAsync(ct);
                return;
            }
            projectIdToCheck = env.ProjectId;
        }

        var projectMember = await _db.ProjectMembers
            .Include(pm => pm.EnvironmentRoles)
            .FirstOrDefaultAsync(pm => pm.ProjectId == projectIdToCheck && pm.UserId == UserId, ct);

        if (projectMember == null)
        {
            await Send.ForbiddenAsync(ct);
            return;
        }

        var effectiveRole = projectMember.Role;
        if (req.EnvironmentId.HasValue)
        {
            var envRoleOverride = projectMember.EnvironmentRoles.FirstOrDefault(er => er.EnvironmentId == req.EnvironmentId.Value);
            if (envRoleOverride != null)
                effectiveRole = envRoleOverride.Role;
        }

        if (effectiveRole == Projects.ProjectRole.None)
        {
            await Send.ForbiddenAsync(ct);
            return;
        }

        var query = _db.AuditLogs.AsNoTracking();

        if (req.EnvironmentId.HasValue)
        {
            query = query.Where(x => x.EnvironmentId == req.EnvironmentId);
        }
        else if (req.ProjectId.HasValue)
        {
            query = query.Where(x => x.ProjectId == req.ProjectId && x.EnvironmentId == null);
        }

        var totalCount = await query.CountAsync(ct);
        var totalPages = (int)Math.Ceiling(totalCount / (double)req.PageSize);

        var logs = await query
            .OrderByDescending(x => x.Timestamp)
            .Skip((req.Page - 1) * req.PageSize)
            .Take(req.PageSize)
            .ToListAsync(ct);

        var response = new GetAuditLogsResponse
        {
            Items = logs.Select(x => new AuditLogDto(
                x.Id,
                x.EnvironmentId,
                x.EntityName,
                x.EntityFriendlyName,
                x.EntityId,
                x.Action,
                x.OldValues,
                x.NewValues,
                x.PerformedByEmail,
                x.Timestamp)).ToList(),
            TotalCount = totalCount,
            TotalPages = totalPages,
            HasPreviousPage = req.Page > 1,
            HasNextPage = req.Page < totalPages
        };

        await Send.OkAsync(response, ct);
    }
}
