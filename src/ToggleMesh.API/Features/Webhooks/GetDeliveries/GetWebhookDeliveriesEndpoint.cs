using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Extensions;
using ToggleMesh.API.Features.Webhooks.Domain;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Endpoints;
using ToggleMesh.Common.Pagination;
using AuthModels = ToggleMesh.API.Infrastructure.Security.Authorization.Models;


namespace ToggleMesh.API.Features.Webhooks.GetDeliveries;

public class GetWebhookDeliveriesEndpoint : ToggleEndpoint<GetWebhookDeliveriesRequest, PagedResponse<WebhookDelivery>>
{
    private readonly AppDbContext _db;

    public GetWebhookDeliveriesEndpoint(AppDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Get("/projects/{projectId:guid}/webhooks/{webhookId:guid}/deliveries");
        Version(1);
        this.RequirePermission(AuthModels.Permissions.ProjectsView);
    }

    public override async Task HandleAsync(GetWebhookDeliveriesRequest req, CancellationToken ct)
    {
        var projectId = Route<Guid>("projectId");
        var webhookId = Route<Guid>("webhookId");

        var webhook = await _db.Webhooks
            .AsNoTracking()
            .FirstOrDefaultAsync(w => 
                w.ProjectId == projectId && w.Id == webhookId, ct);

        if (webhook == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var query = _db.WebhookDeliveries
            .AsNoTracking()
            .Where(d => d.WebhookId == webhookId)
            .OrderByDescending(d => d.CreatedAt);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .Skip((req.Page - 1) * req.PageSize)
            .Take(req.PageSize)
            .ToListAsync(ct);

        await Send.OkAsync(new PagedResponse<WebhookDelivery>
        {
            Items = items,
            TotalCount = totalCount,
            Page = req.Page,
            PageSize = req.PageSize
        }, ct);
    }
}
