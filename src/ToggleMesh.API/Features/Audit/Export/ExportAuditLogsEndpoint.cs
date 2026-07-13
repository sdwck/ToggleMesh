using System.Text;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Features.Audit.Get;
using ToggleMesh.API.Features.Projects.Domain;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Endpoints;
// ReSharper disable NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract

namespace ToggleMesh.API.Features.Audit.Export;

public class ExportAuditLogsEndpoint : ToggleEndpoint<GetAuditLogsRequest>
{
    private readonly AppDbContext _db;

    public ExportAuditLogsEndpoint(AppDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Get("/audit-logs/export");
        Version(1);
    }

    public override async Task HandleAsync(GetAuditLogsRequest req, CancellationToken ct)
    {
        var projectIdToCheck = req.ProjectId ?? Guid.Empty;
        if (req.EnvironmentId.HasValue)
        {
            var env = await _db.Environments
                .FirstOrDefaultAsync(e => e.Id == req.EnvironmentId.Value, ct);
            if (env == null)
            {
                await Send.NotFoundAsync(ct);
                return;
            }

            projectIdToCheck = env.ProjectId;
        }

        var (role, envRoles) = await new GetProjectRoleCommand 
        { 
            ProjectId = projectIdToCheck, 
            UserId = UserId 
        }.ExecuteAsync(ct);

        if (role == null)
        {
            await Send.ForbiddenAsync(ct);
            return;
        }

        var effectiveRole = role.Value;
        if (req.EnvironmentId.HasValue)
            if (envRoles.TryGetValue(req.EnvironmentId.Value, out var envRoleOverride))
                effectiveRole = envRoleOverride;

        if (effectiveRole == ProjectRole.None || effectiveRole == ProjectRole.Viewer)
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
            query = query.Where(x => EF.Functions.ILike(x.Action, req.Action));

        if (!string.IsNullOrWhiteSpace(req.EntityName) && req.EntityName != "all")
            query = query.Where(x => EF.Functions.ILike(x.EntityName, req.EntityName));

        if (!string.IsNullOrWhiteSpace(req.Search))
        {
            var searchTerm = $"%{req.Search}%";
            query = query.Where(x =>
                EF.Functions.ILike(x.EntityFriendlyName, searchTerm) ||
                EF.Functions.ILike(x.EntityId, searchTerm) ||
                EF.Functions.ILike(x.PerformedByEmail, searchTerm));
        }
        
        if (dateFrom.HasValue)
            query = query.Where(x => x.Timestamp >= dateFrom.Value);

        if (dateTo.HasValue)
            query = query.Where(x => x.Timestamp <= dateTo.Value);

        var isAscending = req.SortOrder?.ToLower() == "asc";
        query = isAscending 
            ? query.OrderBy(x => x.Timestamp).ThenBy(x => x.Id) 
            : query.OrderByDescending(x => x.Timestamp).ThenByDescending(x => x.Id);

        var logs = await query.ToListAsync(ct);

        var sb = new StringBuilder();
        sb.AppendLine("Timestamp,PerformedBy,Action,EntityName,EntityFriendlyName,EnvironmentId");

        foreach (var log in logs)
        {
            var timestamp = log.Timestamp.ToString("yyyy-MM-ddTHH:mm:ssZ");
            var performedBy = EscapeCsv(string.IsNullOrEmpty(log.PerformedByEmail) ? "System" : log.PerformedByEmail);
            var action = EscapeCsv(log.Action);
            var entityName = EscapeCsv(log.EntityName);
            var friendlyName = EscapeCsv(string.IsNullOrEmpty(log.EntityFriendlyName) ? log.EntityId : log.EntityFriendlyName);
            var envId = log.EnvironmentId?.ToString() ?? "Project-Level";

            sb.AppendLine($"{timestamp},{performedBy},{action},{entityName},{friendlyName},{envId}");
        }

        var fileName = $"AuditLogs_Export_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";
        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        
        await Send.BytesAsync(bytes, fileName, "text/csv", cancellation: ct);
    }

    private static string EscapeCsv(string field)
    {
        if (string.IsNullOrEmpty(field)) return "";
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n'))
            return $"\"{field.Replace("\"", "\"\"")}\"";
        return field;
    }
}
