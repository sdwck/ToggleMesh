using Microsoft.Extensions.ObjectPool;

namespace ToggleMesh.SDK.Models;

public static class ObjectPools<T>
{
    public static readonly ObjectPool<PooledAnalyticsEvent<T>> Pool =
        new DefaultObjectPool<PooledAnalyticsEvent<T>>(new DefaultPooledObjectPolicy<PooledAnalyticsEvent<T>>(), 10000);
}