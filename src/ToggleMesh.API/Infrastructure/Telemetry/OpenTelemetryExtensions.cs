using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Exporter;

namespace ToggleMesh.API.Infrastructure.Telemetry;

public static class OpenTelemetryExtensions
{
    public static IServiceCollection AddToggleMeshTelemetry(
        this IServiceCollection services,
        IConfiguration configuration,
        ILoggingBuilder logging)
    {
        var serviceName = configuration["Telemetry:ServiceName"] ?? "ToggleMesh.API";
        var serviceVersion = configuration["Telemetry:ServiceVersion"] ?? "1.0.0";
        var otlpEndpoint = configuration["Telemetry:OtlpEndpoint"];
        var otlpProtocol = configuration["Telemetry:OtlpProtocol"];
        var isEnabled = configuration.GetValue("Telemetry:Enabled", true);

        if (!isEnabled)
            return services;

        var resourceBuilder = ResourceBuilder.CreateDefault()
            .AddService(serviceName: serviceName, serviceVersion: serviceVersion);

        services.AddOpenTelemetry()
            .WithTracing(tracing =>
            {
                tracing
                    .SetResourceBuilder(resourceBuilder)
                    .AddSource(ToggleMeshMetrics.MeterName)
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        options.RecordException = true;
                        options.Filter = httpContext => !httpContext.Request.Path.StartsWithSegments("/health");
                    })
                    .AddHttpClientInstrumentation(options => options.RecordException = true)
                    .AddEntityFrameworkCoreInstrumentation(options => options.SetDbStatementForText = true)
                    .AddRedisInstrumentation()
                    .AddQuartzInstrumentation();

                if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                {
                    tracing.AddOtlpExporter(options => ConfigureExporter(options, otlpEndpoint, otlpProtocol, "traces"));
                }
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .SetResourceBuilder(resourceBuilder)
                    .AddMeter(ToggleMeshMetrics.MeterName)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();

                if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                {
                    metrics.AddOtlpExporter(options => ConfigureExporter(options, otlpEndpoint, otlpProtocol, "metrics"));
                }
            });

        logging.AddOpenTelemetry(options =>
        {
            options.SetResourceBuilder(resourceBuilder);
            options.IncludeFormattedMessage = true;
            options.IncludeScopes = true;

            if (!string.IsNullOrWhiteSpace(otlpEndpoint))
            {
                options.AddOtlpExporter(exporterOptions => ConfigureExporter(exporterOptions, otlpEndpoint, otlpProtocol, "logs"));
            }
        });

        return services;
    }

    private static void ConfigureExporter(OtlpExporterOptions options, string endpoint, string? configuredProtocol, string signalType)
    {
        if (!string.IsNullOrWhiteSpace(configuredProtocol))
        {
            if (configuredProtocol.Equals("grpc", StringComparison.OrdinalIgnoreCase))
                options.Protocol = OtlpExportProtocol.Grpc;
            else if (configuredProtocol.Equals("http", StringComparison.OrdinalIgnoreCase) || configuredProtocol.Equals("httpprotobuf", StringComparison.OrdinalIgnoreCase))
                options.Protocol = OtlpExportProtocol.HttpProtobuf;
        }
        else if (endpoint.Contains("/otlp", StringComparison.OrdinalIgnoreCase) || endpoint.EndsWith("/metrics", StringComparison.OrdinalIgnoreCase))
            options.Protocol = OtlpExportProtocol.HttpProtobuf;

        if (options.Protocol == OtlpExportProtocol.HttpProtobuf)
            if (!endpoint.EndsWith($"/v1/{signalType}", StringComparison.OrdinalIgnoreCase) && !endpoint.EndsWith($"/v1/{signalType}/", StringComparison.OrdinalIgnoreCase))
                endpoint = endpoint.TrimEnd('/') + $"/v1/{signalType}";

        options.Endpoint = new Uri(endpoint);
    }
}
