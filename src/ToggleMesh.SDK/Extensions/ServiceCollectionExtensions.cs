using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ToggleMesh.SDK.Clients;
using ToggleMesh.SDK.Contexts;
using ToggleMesh.SDK.Options;
using ToggleMesh.SDK.Rules;
using ToggleMesh.SDK.Rules.Operators;

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

        services.AddSingleton<IRuleOperator, EqualsOperator>();
        services.AddSingleton<IRuleOperator, NotEqualsOperator>();
        services.AddSingleton<IRuleOperator, ContainsOperator>();
        services.AddSingleton<IRuleOperator, StartsWithOperator>();
        services.AddSingleton<IRuleOperator, EndsWithOperator>();
        services.AddSingleton<IRuleOperator, GreaterThanOperator>();
        services.AddSingleton<IRuleOperator, GreaterThanOrEqualOperator>();
        services.AddSingleton<IRuleOperator, LessThanOperator>();
        services.AddSingleton<IRuleOperator, LessThanOrEqualOperator>();
        services.AddSingleton<IRuleOperator, InListOperator>();
        services.AddSingleton<IRuleOperator, SemVerEqualOperator>();
        services.AddSingleton<IRuleOperator, SemVerGreaterThanOperator>();
        services.AddSingleton<IRuleOperator, SemVerGreaterThanOrEqualOperator>();
        services.AddSingleton<IRuleOperator, SemVerLessThanOperator>();
        services.AddSingleton<IRuleOperator, SemVerLessThanOrEqualOperator>();
        services.AddSingleton<IRuleOperator, RegexOperator>();
        services.AddSingleton<IRuleOperator, DateAfterOperator>();
        services.AddSingleton<IRuleOperator, DateBeforeOperator>();
        services.AddSingleton<IRuleEngine, RuleEngine>();
        
        services.AddSingleton<ToggleMeshClient>();
        services.AddSingleton<IToggleMeshClient>(sp => sp.GetRequiredService<ToggleMeshClient>());
        services.AddHostedService(sp => sp.GetRequiredService<ToggleMeshClient>());
        return services;
    }
    
    public static IServiceCollection AddToggleMeshHttpContext(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddSingleton<IToggleMeshContextProvider, HttpContextToggleContextProvider>();
        
        return services;
    }
}