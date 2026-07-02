using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Extensions;
using ToggleMesh.API.Features.Flags.Domain;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Endpoints;
using ToggleMesh.API.Infrastructure.Streaming;
using AuthModels = ToggleMesh.API.Infrastructure.Security.Authorization.Models;


namespace ToggleMesh.API.Features.Projects.CloneEnvironment;

public class CloneEnvironmentEndpoint : ToggleEndpointWithoutRequest
{
    private readonly AppDbContext _db;
    private readonly IToggleEventPublisher _publisher;
    private readonly ILogger<CloneEnvironmentEndpoint> _logger;

    public CloneEnvironmentEndpoint(
        AppDbContext db,
        IToggleEventPublisher publisher,
        ILogger<CloneEnvironmentEndpoint> logger)
    {
        _db = db;
        _publisher = publisher;
        _logger = logger;
    }

    public override void Configure()
    {
        Post("/projects/{projectId:guid}/environments/{sourceEnvId:guid}/clone-to/{targetEnvId:guid}");
        Version(1);
        this.RequirePermission(AuthModels.Permissions.EnvironmentsSync);
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

        if (!await _db.Environments
                .AnyAsync(x =>
                        x.Id == sourceEnvId &&
                        x.ProjectId == projectId,
                    ct))
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var sourceStates = await _db.FlagEnvironmentStates
            .Where(x => x.EnvironmentId == sourceEnvId)
            .Include(x => x.Rules)
            .AsNoTracking()
            .ToListAsync(ct);

        var targetStates = await _db.FlagEnvironmentStates
            .Where(x => x.EnvironmentId == targetEnvId)
            .Include(x => x.Rules)
            .ToListAsync(ct);

        foreach (var sourceState in sourceStates)
        {
            var targetState = targetStates.FirstOrDefault(x => x.FeatureFlagId == sourceState.FeatureFlagId);
            if (targetState == null)
            {
                targetState = new FlagEnvironmentState
                {
                    EnvironmentId = targetEnvId,
                    FeatureFlagId = sourceState.FeatureFlagId
                };
                _db.FlagEnvironmentStates.Add(targetState);
            }

            targetState.IsEnabled = sourceState.IsEnabled;
            targetState.RolloutPercentage = sourceState.RolloutPercentage;
            
            if (targetState.Rules.Count != 0)
            {
                _db.FlagRules.RemoveRange(targetState.Rules);
                targetState.Rules.Clear();
            }
            else
            {
                targetState.Rules = new List<FlagRule>();
            }
            
            foreach (var rule in sourceState.Rules)
            {
                targetState.Rules.Add(new FlagRule
                {
                    GroupId = rule.GroupId,
                    Attribute = rule.Attribute,
                    Operator = rule.Operator,
                    Value = rule.Value
                });
            }
        }

        await _db.SaveChangesAsync(ct);

        try
        {
            await _publisher.PublishEventAsync<object?>(targetEnvId.ToString(), "StateReloadRequired", null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to notify clients via SignalR about environment clone.");
        }

        await Send.NoContentAsync(ct);
    }
}
