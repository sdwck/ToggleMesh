using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Infrastructure;
using ToggleMesh.API.Infrastructure.Endpoints;
using ToggleMesh.API.Persistence;
using ToggleMesh.Common.Pagination;

namespace ToggleMesh.API.Features.Audit.Get;

public class GetAuditLogsEndpoint : ToggleEndpoint<GetAuditLogsRequest, CursorPagedResponse<AuditLogDto>>
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
        var projectIdToCheck = req.ProjectId ?? Guid.Empty;
        if (req.EnvironmentId.HasValue)
        {
            var env = await _db.Environments
                .FirstOrDefaultAsync(e =>
                    e.Id == req.EnvironmentId.Value, ct);
            if (env == null)
            {
                await Send.NotFoundAsync(ct);
                return;
            }

            projectIdToCheck = env.ProjectId;
        }

        var (role, envRoles) = await _db.GetProjectRoleAndEnvOverridesAsync(projectIdToCheck, UserId, ct);

        if (role == null)
        {
            await Send.ForbiddenAsync(ct);
            return;
        }

        var effectiveRole = role.Value;
        if (req.EnvironmentId.HasValue)
        {
            if (envRoles.TryGetValue(req.EnvironmentId.Value, out var envRoleOverride))
                effectiveRole = envRoleOverride;
        }

        if (effectiveRole == Projects.ProjectRole.None)
        {
            await Send.ForbiddenAsync(ct);
            return;
        }
        
        DateTime? dateFrom = null;
        if (req.DateFrom.HasValue)
            dateFrom = req.DateFrom.Value.Kind == DateTimeKind.Local 
                ? req.DateFrom.Value.ToUniversalTime() 
                : DateTime.SpecifyKind(req.DateFrom.Value, DateTimeKind.Utc);
        

        DateTime? dateTo = null;
        if (req.DateTo.HasValue)
        {
            dateTo = req.DateTo.Value.Kind == DateTimeKind.Local 
                ? req.DateTo.Value.ToUniversalTime() 
                : DateTime.SpecifyKind(req.DateTo.Value, DateTimeKind.Utc);

            if (dateTo.Value.TimeOfDay == TimeSpan.Zero)
                dateTo = dateTo.Value.AddDays(1).AddTicks(-1);
        }

        var query = _db.AuditLogs.AsNoTracking();

        if (req.EnvironmentId.HasValue)
            query = query.Where(x => x.EnvironmentId == req.EnvironmentId);

        else if (req.ProjectId.HasValue)
            query = query.Where(x => x.ProjectId == req.ProjectId && x.EnvironmentId == null);

        if (!string.IsNullOrWhiteSpace(req.Action) && req.Action != "all")
            query = query.Where(x => 
                EF.Functions.ILike(x.Action, req.Action));

        if (!string.IsNullOrWhiteSpace(req.EntityName) && req.EntityName != "all")
            query = query.Where(x => 
                EF.Functions.ILike(x.EntityName, req.EntityName));
        
        if (dateFrom.HasValue)
            query = query.Where(x => x.Timestamp >= dateFrom.Value);

        if (dateTo.HasValue)
            query = query.Where(x => x.Timestamp <= dateTo.Value);

        var isAscending = req.SortOrder?.ToLower() == "asc";
        query = isAscending 
            ? query.OrderBy(x => x.Id) 
            : query.OrderByDescending(x => x.Id);

        if (req.Cursor.HasValue)
        {
            if (isAscending)
                query = query.Where(x => x.Id > req.Cursor.Value);
            else
                query = query.Where(x => x.Id < req.Cursor.Value);
        }

        var totalCount = await query.CountAsync(ct);

        var logs = await query
            .Take(req.PageSize + 1)
            .ToListAsync(ct);

        var hasNextPage = logs.Count > req.PageSize;
        if (hasNextPage)
            logs.RemoveAt(logs.Count - 1);

        var nextCursor = logs.Count > 0 ? logs.Last().Id : (Guid?)null;

        var items = logs.Select(x => new AuditLogDto(
            x.Id,
            x.EnvironmentId,
            x.EntityName,
            x.EntityFriendlyName,
            x.EntityId,
            x.Action,
            x.OldValues,
            x.NewValues,
            x.PerformedByEmail,
            x.Timestamp)).ToList();

        await Send.OkAsync(new CursorPagedResponse<AuditLogDto>(items, totalCount, nextCursor, hasNextPage), ct);
    }
}