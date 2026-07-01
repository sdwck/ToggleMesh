using Microsoft.Extensions.DependencyInjection;
using ToggleMesh.Common.Rules;

namespace ToggleMesh.SDK.Clients;

internal sealed class SdkSegmentProviderProxy : ISegmentProvider
{
    private readonly IServiceProvider _serviceProvider;

    public SdkSegmentProviderProxy(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public CompiledRuleGroup[]? GetSegmentRules(string segmentKey)
    {
        var client = _serviceProvider.GetRequiredService<ToggleMeshClient>();
        return client.GetSegmentRules(segmentKey);
    }
}
