using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Features.Projects;
using ToggleMesh.API.Infrastructure.Security;
using ToggleMesh.API.Persistence;

namespace ToggleMesh.API.Infrastructure;

// ReSharper disable once ClassNeverInstantiated.Global
public class ApiKeyPreProcessor<TRequest> : IPreProcessor<TRequest> 
    where TRequest : class, ISdkRequest
{
    public async Task PreProcessAsync(IPreProcessorContext<TRequest> context, CancellationToken ct)
    {
        var httpContext = context.HttpContext;
        var apiKey = httpContext.Request.Headers["x-api-key"].ToString();
        var patToken = httpContext.Request.Headers["x-pat-token"].ToString();
        var envIdStr = httpContext.Request.Headers["x-environment-id"].ToString();

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            await HandleApiKeyAsync(context, apiKey, ct);
            return;
        }

        if (!string.IsNullOrWhiteSpace(patToken))
        {
            await HandlePatTokenAsync(context, patToken, envIdStr, ct);
            return;
        }

        await httpContext.Response.SendUnauthorizedAsync(ct);
    }

    private async Task HandleApiKeyAsync(IPreProcessorContext<TRequest> context, string apiKey, CancellationToken ct)
    {
        var httpContext = context.HttpContext;
        var apiKeyCache = httpContext.RequestServices.GetRequiredService<IApiKeyCacheService>();

        var keyInfo = await apiKeyCache.GetKeyInfoAsync(apiKey, ct);
        if (keyInfo is null)
        {
            await httpContext.Response.SendUnauthorizedAsync(ct);
            return;
        }

        context.Request?.EnvId = keyInfo.EnvironmentId;
        context.Request?.KeyType = keyInfo.KeyType;
    }

    private async Task HandlePatTokenAsync(IPreProcessorContext<TRequest> context, string patToken, string envIdStr, CancellationToken ct)
    {
        var httpContext = context.HttpContext;
        if (!Guid.TryParse(envIdStr, out var envId))
        {
            await httpContext.Response.SendUnauthorizedAsync(ct);
            return;
        }

        var db = httpContext.RequestServices.GetRequiredService<AppDbContext>();
        var tokenHash = ApiKeyHasher.Hash(patToken);
        
        var pat = await db.PersonalAccessTokens
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.TokenHash == tokenHash && (x.ExpiresAt == null || x.ExpiresAt > DateTime.UtcNow), ct);

        if (pat is null)
        {
            await httpContext.Response.SendUnauthorizedAsync(ct);
            return;
        }

        var hasAccess = await CheckUserEnvironmentAccessAsync(db, pat.UserId, envId, ct);
        if (!hasAccess)
        {
            await httpContext.Response.SendForbiddenAsync(ct);
            return;
        }

        await db.PersonalAccessTokens
            .Where(x => x.Id == pat.Id)
            .ExecuteUpdateAsync(s => 
                s.SetProperty(t => t.LastUsedAt, DateTime.UtcNow), ct);

        context.Request?.EnvId = envId;
        context.Request?.KeyType = KeyType.Server;
    }

    private async Task<bool> CheckUserEnvironmentAccessAsync(AppDbContext db, Guid userId, Guid envId, CancellationToken ct)
    {
        var env = await db.Environments
            .AsNoTracking()
            .Select(e => new { e.Id, e.ProjectId })
            .FirstOrDefaultAsync(e => e.Id == envId, ct);

        if (env is null) 
            return false;

        var member = await db.ProjectMembers
            .AsNoTracking()
            .Include(pm => pm.EnvironmentRoles)
            .FirstOrDefaultAsync(pm => 
                pm.ProjectId == env.ProjectId && 
                pm.UserId == userId, ct);

        if (member is null) 
            return false;
        
        if (member.Role is ProjectRole.Owner or ProjectRole.Admin)
            return true;

        var envRole = member.EnvironmentRoles.FirstOrDefault(er => 
            er.EnvironmentId == envId);
        if (envRole is null)
            return member.Role != ProjectRole.None;

        return envRole.Role != ProjectRole.None;
    }
}