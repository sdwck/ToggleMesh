using FastEndpoints;
using FluentValidation;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Hubs;
using ToggleMesh.API.Persistence;

namespace ToggleMesh.API.Features.Projects.CloneEnvironment;

public class CloneEnvironmentEndpoint : EndpointWithoutRequest
{
    private readonly AppDbContext _db;
    private readonly IHubContext<ToggleHub> _hubContext;
    private readonly ILogger<CloneEnvironmentEndpoint> _logger;

    public CloneEnvironmentEndpoint(
        AppDbContext db,
        IHubContext<ToggleHub> hubContext,
        ILogger<CloneEnvironmentEndpoint> logger)
    {
        _db = db;
        _hubContext = hubContext;
        _logger = logger;
    }

    public override void Configure()
    {
        Post("/projects/{projectId:guid}/environments/{sourceEnvId:guid}/clone-to/{targetEnvId:guid}");
        Version(1);
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var projectId = Route<Guid>("projectId");
        var sourceEnvId = Route<Guid>("sourceEnvId");
        var targetEnvId = Route<Guid>("targetEnvId");

        if (!await _db.Environments
                .AnyAsync(x =>
                        x.Id == targetEnvId &&
                        x.ProjectId == projectId,
                    ct))
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var sourceFlags = await _db.FeatureFlags
            .Where(x => x.EnvironmentId == sourceEnvId)
            .Include(x => x.Rules)
            .AsNoTracking()
            .ToListAsync(ct);

        var oldFlags = await _db.FeatureFlags
            .Where(x => x.EnvironmentId == targetEnvId)
            .ToListAsync(ct);
        _db.FeatureFlags.RemoveRange(oldFlags);

        foreach (var featureFlag in sourceFlags)
        {
            featureFlag.Id = 0;
            featureFlag.EnvironmentId = targetEnvId;
            foreach (var rule in featureFlag.Rules)
                rule.Id = 0;
        }

        _db.FeatureFlags.AddRange(sourceFlags);
        await _db.SaveChangesAsync(ct);

        try
        {
            await _hubContext.Clients.Group(targetEnvId.ToString()).SendAsync("StateReloadRequired", ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to notify clients via SignalR about environment clone.");
        }

        await Send.NoContentAsync(ct);
    }
}