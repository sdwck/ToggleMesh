using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ToggleMesh.SDK.Clients;
using ToggleMesh.SDK.Options;

namespace ToggleMesh.SDK.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddToggleMeshClient(this IServiceCollection services, Action<ToggleMeshOptions> options)
    {
        services.AddOptions<ToggleMeshOptions>()
            .Configure(options)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        
        services.AddHttpClient("ToggleMesh", (sp, client) => {
            var o = sp.GetRequiredService<IOptions<ToggleMeshOptions>>().Value;
            client.BaseAddress = new Uri(o.EndpointUrl);
            client.DefaultRequestHeaders.Add("x-api-key", o.ApiKey);
        }).RemoveAllLoggers();
        
        services.AddSingleton<ToggleMeshClient>();
        services.AddSingleton<IToggleMeshClient>(sp => sp.GetRequiredService<ToggleMeshClient>());
        services.AddHostedService(sp => sp.GetRequiredService<ToggleMeshClient>());
        return services;
    }
}