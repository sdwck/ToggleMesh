namespace ToggleMesh.API.Infrastructure.Caching;

public interface ICacheInvalidator
{
    Task InvalidateEnvironmentCacheAsync(Guid envId);
}