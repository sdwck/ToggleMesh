using FastEndpoints;

namespace ToggleMesh.API.Infrastructure;

public class ApiKeyPreProcessor<TRequest> : IPreProcessor<TRequest> 
    where TRequest : ISdkRequest
{
    public async Task PreProcessAsync(IPreProcessorContext<TRequest> context, CancellationToken ct)
    {
        var req = context.Request;

        if (req is null || string.IsNullOrWhiteSpace(req.ApiKey))
        {
            await context.HttpContext.Response.SendUnauthorizedAsync(ct);
            return;
        }
        
        var apiKeyCache = context.HttpContext.RequestServices.GetRequiredService<IApiKeyCacheService>();
        var keyInfo = await apiKeyCache.GetKeyInfoAsync(req.ApiKey, ct);

        if (keyInfo is null)
        {
            await context.HttpContext.Response.SendUnauthorizedAsync(ct);
            return;
        }

        req.EnvId = keyInfo.EnvironmentId;
        req.KeyType = keyInfo.KeyType;
    }
}