using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ToggleMesh.Common.Rules;
using ToggleMesh.SDK.Clients;
using ToggleMesh.SDK.Options;
using ToggleMesh.Common.Rules.Operators;
using System.Collections.Concurrent;
using ToggleMesh.Common;

namespace ToggleMesh.IntegrationTests.Sdk;

public class SdkEvaluationTests
{
    private readonly ToggleMeshClient _client;
    private readonly ConcurrentDictionary<string, CachedFlag> _cache;

    public SdkEvaluationTests()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IRuleOperator, EqualsOperator>();
        var sp = services.BuildServiceProvider();

        var engine = new RuleEngine(sp.GetServices<IRuleOperator>());
        
        var options = Microsoft.Extensions.Options.Options.Create(new ToggleMeshOptions());
        
        var dummyHttpFactory = new DummyHttpClientFactory();

        _client = new ToggleMeshClient(
            dummyHttpFactory,
            options,
            NullLogger<ToggleMeshClient>.Instance,
            engine,
            []
        );

        var field = typeof(ToggleMeshClient).GetField("_cache", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        _cache = (ConcurrentDictionary<string, CachedFlag>)field!.GetValue(_client)!;
    }

    [Fact]
    public void GetStringVariation_WhenFlagExists_ShouldReturnVariationValue()
    {
        // Arrange
        var varId = Guid.NewGuid();
        _cache["color_flag"] = new CachedFlag
        {
            Key = "color_flag",
            IsEnabled = true,
            FallthroughRollout = [new VariationWeight(varId, 10000)],
            Variations = new Dictionary<Guid, string> { { varId, "red" } }
        };

        // Act
        var result = _client.GetStringVariation("color_flag", "blue");

        // Assert
        result.Should().Be("red");
    }

    [Fact]
    public void GetStringVariation_WhenFlagIsDisabled_ShouldReturnDefaultValue()
    {
        // Arrange
        var varId = Guid.NewGuid();
        _cache["color_flag"] = new CachedFlag
        {
            Key = "color_flag",
            IsEnabled = false,
            FallthroughRollout = [new VariationWeight(varId, 10000)],
            Variations = new Dictionary<Guid, string> { { varId, "red" } }
        };

        // Act
        var result = _client.GetStringVariation("color_flag", "blue");

        // Assert
        result.Should().Be("blue");
    }

    [Fact]
    public void GetJsonVariation_WhenFlagExists_ShouldReturnParsedJson()
    {
        // Arrange
        var varId = Guid.NewGuid();
        _cache["config_flag"] = new CachedFlag
        {
            Key = "config_flag",
            IsEnabled = true,
            FallthroughRollout = [new VariationWeight(varId, 10000)],
            Variations = new Dictionary<Guid, string> { { varId, "{\"Timeout\": 5000}" } }
        };

        // Act
        var result = _client.GetJsonVariation(
            "config_flag", 
            new TestConfig { Timeout = 1000 });

        // Assert
        result.Should().NotBeNull();
        result.Timeout.Should().Be(5000);
    }
    
    private class TestConfig
    {
        public int Timeout { get; set; }
    }

    private class DummyHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }
}
