using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using ToggleMesh.API.Infrastructure.Telemetry;

namespace ToggleMesh.IntegrationTests.Infrastructure;

public class TelemetryAndHealthCheckTests
{
    [Fact]
    public void AddToggleMeshTelemetry_RegistersOpenTelemetryServices_WhenEnabled()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Telemetry:Enabled"] = "true",
                ["Telemetry:ServiceName"] = "TestService"
            })
            .Build();

        var logging = new TestLoggingBuilder(services);

        services.AddToggleMeshTelemetry(configuration, logging);

        var provider = services.BuildServiceProvider();
        var tracerProvider = provider.GetService<TracerProvider>();
        var meterProvider = provider.GetService<MeterProvider>();

        tracerProvider.Should().NotBeNull();
        meterProvider.Should().NotBeNull();
    }

    [Fact]
    public void ToggleMeshMetrics_IncrementsCountersCorrectly()
    {
        using var meterListener = new System.Diagnostics.Metrics.MeterListener();
        long sseCounter = 0;
        long cacheCounter = 0;

        meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == ToggleMeshMetrics.MeterName)
                listener.EnableMeasurementEvents(instrument);
        };

        meterListener.SetMeasurementEventCallback<long>((instrument, measurement, _, _) =>
        {
            if (instrument.Name == ToggleMeshMetrics.ActiveSseConnections.Name)
                sseCounter += measurement;
            else if (instrument.Name == ToggleMeshMetrics.CacheRequests.Name)
                cacheCounter += measurement;
        });

        meterListener.Start();

        ToggleMeshMetrics.ActiveSseConnections.Add(1, new KeyValuePair<string, object?>("env", "test"));
        ToggleMeshMetrics.ActiveSseConnections.Add(-1, new KeyValuePair<string, object?>("env", "test"));
        ToggleMeshMetrics.CacheRequests.Add(1, new KeyValuePair<string, object?>("type", "hit"));

        sseCounter.Should().Be(0);
        cacheCounter.Should().Be(1);
    }

    private class TestLoggingBuilder : ILoggingBuilder
    {
        public TestLoggingBuilder(IServiceCollection services)
        {
            Services = services;
        }

        public IServiceCollection Services { get; }
    }
}
