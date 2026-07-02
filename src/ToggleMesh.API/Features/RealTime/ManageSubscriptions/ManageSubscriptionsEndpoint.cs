using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Features.Projects.Domain;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Endpoints;
using ToggleMesh.API.Infrastructure.Sse;

namespace ToggleMesh.API.Features.RealTime.ManageSubscriptions;

public class ManageSubscriptionsEndpoint : ToggleEndpoint<ManageSubscriptionsRequest>
{
    private readonly ISseService _sseService;
    private readonly AppDbContext _db;

    public ManageSubscriptionsEndpoint(ISseService sseService, AppDbContext db)
    {
        _sseService = sseService;
        _db = db;
    }

    public override void Configure()
    {
        Post("/realtime/subscriptions");
        Version(1);
    }

    public override async Task HandleAsync(ManageSubscriptionsRequest req, CancellationToken ct)
    {
        if (req.ConnectionId == Guid.Empty || string.IsNullOrEmpty(req.Topic))
        {
            await Send.ErrorsAsync(400, cancellation: ct);
            return;
        }

        if (!_sseService.VerifyConnectionOwner(req.ConnectionId, UserId))
        {
            await Send.ForbiddenAsync(cancellation: ct);
            return;
        }

        if (req.Action == "unsubscribe")
        {
            _sseService.UnsubscribeTopic(req.ConnectionId, req.Topic);
            await Send.OkAsync(cancellation: ct);
            return;
        }

        if (req.Action != "subscribe")
        {
            await Send.ErrorsAsync(400, cancellation: ct);
            return;
        }

        if (req.Topic.StartsWith("livetail:"))
        {
            if (!Guid.TryParse(req.Topic.Substring("livetail:".Length), out var envId))
            {
                await Send.ErrorsAsync(400, cancellation: ct);
                return;
            }

            var projectInfo = await _db.Environments
                .AsNoTracking()
                .Where(e => e.Id == envId)
                .Select(e => new { e.ProjectId })
                .FirstOrDefaultAsync(ct);

            if (projectInfo == null)
            {
                await Send.NotFoundAsync(ct);
                return;
            }

            var (role, _) = await new GetProjectRoleCommand 
            { 
                ProjectId = projectInfo.ProjectId, 
                UserId = UserId 
            }.ExecuteAsync(ct);
            
            if (role == null)
            {
                await Send.ForbiddenAsync(cancellation: ct);
                return;
            }
        }

        _sseService.SubscribeTopic(req.ConnectionId, req.Topic);
        await Send.OkAsync(cancellation: ct);
    }
}
