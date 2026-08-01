using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace ToggleMesh.API.Infrastructure.Telemetry;

public static class HealthCheckExtensions
{
    public static IServiceCollection AddToggleMeshHealthChecks(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var dbConnectionString = configuration.GetConnectionString("DefaultConnection");
        var redisConnectionString = configuration.GetConnectionString("Redis");

        var healthChecksBuilder = services.AddHealthChecks();

        if (!string.IsNullOrWhiteSpace(dbConnectionString))
        {
            healthChecksBuilder.AddNpgSql(
                dbConnectionString,
                name: "postgres",
                tags: new[] { "db", "ready" });
        }

        if (!string.IsNullOrWhiteSpace(redisConnectionString))
        {
            healthChecksBuilder.AddRedis(
                redisConnectionString,
                name: "redis",
                tags: new[] { "cache", "ready" });
        }

        return services;
    }

    public static IEndpointRouteBuilder MapToggleMeshHealthChecks(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks("/health", new HealthCheckOptions
        {
            Predicate = _ => true,
            ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
        });

        endpoints.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false
        });

        endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
            ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
        });

        return endpoints;
    }
}
