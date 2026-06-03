using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.NoEmit;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using ToggleMesh.SDK.Clients;
using ToggleMesh.SDK.Options;
using ToggleMesh.SDK.Rules;
using ToggleMesh.SDK.Rules.Operators;
using System.Reflection;

namespace ToggleMesh.Benchmarks;

public class Config : ManualConfig
{
    public Config()
    {
        AddJob(Job.Default.WithToolchain(InProcessNoEmitToolchain.Instance));
    }
}

[Config(typeof(Config))]
[MemoryDiagnoser]
public class IsEnabledBenchmark
{
    private ToggleMeshClient _client = null!;
    private readonly Dictionary<string, string> _userContext = new() { { "Email", "test@gmail.com" } };

    [GlobalSetup]
    public void Setup()
    {
        var operators = new IRuleOperator[] { new EqualsOperator(), new EndsWithOperator() };
        var engine = new RuleEngine(operators);
        var httpClientFactory = new Mock<IHttpClientFactory>();
        var options = Options.Create(new ToggleMeshOptions { ApiKey = "bench", EndpointUrl = "http://localhost" });
        
        _client = new ToggleMeshClient(
            httpClientFactory.Object, 
            options, 
            NullLogger<ToggleMeshClient>.Instance, 
            engine, 
            []);

        var cacheField = typeof(ToggleMeshClient).GetField("_cache", BindingFlags.NonPublic | BindingFlags.Instance);
        var cacheInstance = cacheField!.GetValue(_client)!;

        var dtoType = typeof(ToggleMeshClient).GetNestedType("FeatureFlagDto", BindingFlags.NonPublic);
        var flagData = new { 
            Key = "bench-flag", 
            IsEnabled = true, 
            Rules = new[] { new { GroupId = 1, Attribute = "Email", Operator = "EndsWith", Value = "@gmail.com" } },
            RolloutPercentage = (int?)null 
        };

        var dto = System.Text.Json.JsonSerializer.Deserialize(
            System.Text.Json.JsonSerializer.Serialize(flagData), 
            dtoType!,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        
        var tryAddMethod = cacheInstance.GetType().GetMethod("TryAdd", [typeof(string), dtoType!]);
        
        if (tryAddMethod == null)
            throw new Exception("Could not find TryAdd method on cache instance.");

        tryAddMethod.Invoke(cacheInstance, ["bench-flag", dto!]);
    }

    [Benchmark]
    public bool IsEnabled_WithRules()
    {
        return _client.IsEnabled("bench-flag", _userContext);
    }
}