using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Extensions;
using ToggleMesh.API.Features.Flags.Domain;
using ToggleMesh.API.Features.Flags.Get;
using ToggleMesh.API.Features.Flags.GetAll;
using ToggleMesh.API.Infrastructure.Caching;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Endpoints;
using AuthModels = ToggleMesh.API.Infrastructure.Security.Authorization.Models;

namespace ToggleMesh.API.Features.Flags.Create;

public class CreateFlagEndpoint : ToggleEndpoint<CreateFlagRequest, GetFlagResponse>
{
    private readonly AppDbContext _db;
    private readonly ICacheInvalidator _cacheInvalidator;

    public CreateFlagEndpoint(
        AppDbContext db,
        ICacheInvalidator cacheInvalidator)
    {
        _db = db;
        _cacheInvalidator = cacheInvalidator;
    }

    public override void Configure()
    {
        Post("/projects/{projectId}/flags");
        Version(1);
        this.RequirePermission(AuthModels.Permissions.FlagsCreate);
    }

    public override async Task HandleAsync(CreateFlagRequest req, CancellationToken ct)
    {
        var projectId = Route<Guid>("projectId");
        
        var exists = await _db.FeatureFlags
            .AnyAsync(x => x.ProjectId == projectId && x.Key == req.Key, ct);
        
        if (exists)
        {
            AddError(x => x.Key, 
                "A feature flag with this key already exists in this project.");
            await Send.ErrorsAsync(cancellation: ct);
            return;
        }

        var newFlag = new FeatureFlag
        {
            ProjectId = projectId,
            Key = req.Key,
            CreatedAt = DateTime.UtcNow,
            Tags = req.Tags.ToArray(),
            Type = req.Type,
            Variations = req.Type == 0 && (req.Variations == null || req.Variations.Count == 0) 
                ? [
                    new FlagVariation
                    {
                        Id = Guid.CreateVersion7(), 
                        Key = "true", 
                        Name = "True", 
                        Value = "true", 
                        Sequence = 0
                    },
                    new FlagVariation
                    {
                        Id = Guid.CreateVersion7(), 
                        Key = "false", 
                        Name = "False", 
                        Value = "false", 
                        Sequence = 1
                    }
                ]
                : req.Variations?.Select((v, i) => 
                    new FlagVariation
                    {
                        Id = v.Id, 
                        Key = v.Id.ToString(), 
                        Name = v.Id.ToString(), 
                        Value = v.Value, 
                        Sequence = i
                    }).ToList() ?? []
        };
        
        _db.FeatureFlags.Add(newFlag);
        
        var environments = await _db.Environments
            .Where(e => e.ProjectId == projectId)
            .Select(e => e.Id)
            .ToListAsync(ct);
        
        var safeRules = req.Rules;

        var responseDefaultRollout = req.FallthroughRollout.Count != 0
            ? req.FallthroughRollout.Select(r => new VariationWeight { VariationId = r.VariationId, Weight = r.Weight }).ToList() 
            : newFlag.Variations.Select((v, i) => new VariationWeight 
            { 
                VariationId = v.Id, 
                Weight = i == 0 ? 10000 : 0 
            }).ToList();
        
        foreach (var envId in environments)
        {
            var envDefaultRollout = req.FallthroughRollout != null && req.FallthroughRollout.Count != 0
                ? req.FallthroughRollout.Select(r => new VariationWeight { VariationId = r.VariationId, Weight = r.Weight }).ToList() 
                : newFlag.Variations.Select((v, i) => new VariationWeight 
                { 
                    VariationId = v.Id, 
                    Weight = i == 0 ? 10000 : 0 
                }).ToList();

            var state = new FlagEnvironmentState
            {
                FeatureFlag = newFlag,
                EnvironmentId = envId,
                IsEnabled = false,
                OffVariationId = req.OffVariationId,
                FallthroughRollout = envDefaultRollout,
                Rules = safeRules.Select(r => new FlagRule
                {
                    GroupId = r.GroupId,
                    Attribute = r.Attribute,
                    Operator = r.Operator,
                    Value = r.Value
                }).ToList()
            };
            _db.FlagEnvironmentStates.Add(state);
        }
        
        await _db.SaveChangesAsync(ct);
        
        foreach (var envId in environments)
            await _cacheInvalidator.InvalidateEnvironmentCacheAsync(envId);

        var response = new GetFlagResponse(
            newFlag.Key, 
            false, 
            safeRules,
            newFlag.Tags,
            req.OffVariationId,
            responseDefaultRollout,
            0, 
            0, 
            false, 
            null, 
            false, 
            null, 
            MabOptimizationType.Conversion, 
            null, 
            null,
            req.Variations,
            5,
            null,
            newFlag.Type);

        await Send.CreatedAtAsync<GetFlagsEndpoint>(
            routeValues: new { projectId },
            responseBody: response,
            cancellation: ct);
    }
}
