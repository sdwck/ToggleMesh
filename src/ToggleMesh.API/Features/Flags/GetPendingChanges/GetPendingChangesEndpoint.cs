using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Endpoints;
using ToggleMesh.API.Features.Flags.Domain;
using ToggleMesh.API.Extensions;
using ToggleMesh.Common.Pagination;
using AuthModels = ToggleMesh.API.Infrastructure.Security.Authorization.Models;

namespace ToggleMesh.API.Features.Flags.GetPendingChanges;

public class GetPendingChangesEndpoint : ToggleEndpoint<GetPendingChangesRequest, CursorPagedResponse<PendingChangeDto>>
{
    private readonly AppDbContext _db;

    public GetPendingChangesEndpoint(AppDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Get("/projects/{projectId}/flags/{key}/environments/{environmentId}/changes");
        Version(1);
        this.RequirePermission(AuthModels.Permissions.FlagsView);
    }

    public override async Task HandleAsync(GetPendingChangesRequest req, CancellationToken ct)
    {
        var flag = await _db.FeatureFlags
            .FirstOrDefaultAsync(x => x.ProjectId == req.ProjectId && x.Key == req.Key, ct);

        if (flag is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var query = _db.PendingFlagChanges
            .AsNoTracking()
            .Include(x => x.RequestedByUser)
            .Include(x => x.ReviewedByUser)
            .Where(x => x.FlagId == flag.Id && x.EnvironmentId == req.EnvironmentId);

        if (req.DateFrom.HasValue)
        {
            var dateFrom = req.DateFrom.Value.Kind == DateTimeKind.Local
                ? req.DateFrom.Value.ToUniversalTime()
                : DateTime.SpecifyKind(req.DateFrom.Value, DateTimeKind.Utc);
            query = query.Where(x => x.CreatedAt >= dateFrom);
        }

        if (req.DateTo.HasValue)
        {
            var dateTo = req.DateTo.Value.Kind == DateTimeKind.Local
                ? req.DateTo.Value.ToUniversalTime()
                : DateTime.SpecifyKind(req.DateTo.Value, DateTimeKind.Utc);
            if (dateTo.TimeOfDay == TimeSpan.Zero)
                dateTo = dateTo.AddDays(1).AddTicks(-1);
            query = query.Where(x => x.CreatedAt <= dateTo);
        }

        if (req.ExcludePurelyScheduled)
            query = query.Where(x => !x.IsPurelyScheduled);

        if (!string.IsNullOrWhiteSpace(req.Status) && req.Status != "all")
        {
            if (req.Status == "active")
                query = query.Where(x => x.Status == PendingFlagChangeStatus.PendingReview || x.Status == PendingFlagChangeStatus.Scheduled);
            else if (req.Status == "history")
                query = query.Where(x => x.Status == PendingFlagChangeStatus.Executed || x.Status == PendingFlagChangeStatus.Cancelled || x.Status == PendingFlagChangeStatus.Expired || x.Status == PendingFlagChangeStatus.Rejected);
        }

        query = query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id);

        if (!string.IsNullOrEmpty(req.Cursor))
        {
            try
            {
                var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(req.Cursor));
                var parts = decoded.Split('_');
                if (parts.Length == 2 &&
                    DateTime.TryParse(parts[0], null, DateTimeStyles.RoundtripKind, out var cursorTime) &&
                    Guid.TryParse(parts[1], out var cursorId))
                    query = query.Where(x => x.CreatedAt < cursorTime || (x.CreatedAt == cursorTime && x.Id < cursorId));
            }
            catch
            {
                // ignore
            }
        }

        var totalCount = await query.CountAsync(ct);
        var pageSize = Math.Clamp(req.PageSize, 1, 50);

        var list = await query
            .Take(pageSize + 1)
            .ToListAsync(ct);

        var hasNextPage = list.Count > pageSize;
        if (hasNextPage)
            list.RemoveAt(list.Count - 1);

        string? nextCursor = null;
        if (list.Count > 0)
        {
            var last = list.Last();
            var raw = $"{last.CreatedAt:O}_{last.Id}";
            nextCursor = Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
        }

        var items = list.Select(x =>
        {
            var effectiveStatus = x.Status;
            if (effectiveStatus == PendingFlagChangeStatus.PendingReview && x.ExecuteAt.HasValue && x.ExecuteAt.Value <= DateTimeOffset.UtcNow)
                effectiveStatus = PendingFlagChangeStatus.Expired;

            return new PendingChangeDto(
            x.Id,
            x.FlagId,
            x.EnvironmentId,
            x.RequestedByUserId,
            x.RequestedByUser.UserName ?? x.RequestedByUser.Email ?? "Unknown",
            x.RequestedByUser.Email ?? "",
            x.ReviewedByUserId,
            x.ReviewedByUser != null ? (x.ReviewedByUser.UserName ?? x.ReviewedByUser.Email) : null,
            x.ReviewedByUser?.Email,
            x.ApprovedByUserIds,
            effectiveStatus.ToString(),
            x.PatchInstructionsJson,
            x.DiffSummaryJson,
            x.ExecuteAt,
            x.IsPurelyScheduled,
            x.Comment,
            x.CreatedAt
        );
        }).ToList();

        await Send.OkAsync(new CursorPagedResponse<PendingChangeDto>(
            items, totalCount, nextCursor, hasNextPage), ct);
    }
}