using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.NoEmit;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using ToggleMesh.SDK.Clients;
using ToggleMesh.SDK.Options;
using System.Reflection;
using ToggleMesh.Common;
using ToggleMesh.Common.Rules;
using ToggleMesh.Common.Rules.Operators;

namespace ToggleMesh.Benchmarks;

public class TypedUserContext
{
    public string Email { get; set; } = "test@gmail.com";
    public int Age { get; set; } = 25;
}

[MemoryDiagnoser]
public class IsEnabledBenchmark
{
    private ToggleMeshClient _client = null!;
    private readonly TypedUserContext _userContextTyped = new();
    private readonly Dictionary<string, string> _userContextDictionary = new() { { "Email", "test@gmail.com" } };

    [GlobalSetup]
    public void Setup()
    {
        var operators = new IRuleOperator[] { new EqualsOperator(), new EndsWithOperator() };
        var engine = new RuleEngine(operators);
        var httpClientFactory = new Mock<IHttpClientFactory>();
        var options = Options.Create(new ToggleMeshOptions { ApiKey = "bench", BaseUrl = "http://localhost" });

        _client = new ToggleMeshClient(
            httpClientFactory.Object,
            options,
            NullLogger<ToggleMeshClient>.Instance,
            engine,
            []);

        var dtoType = typeof(FeatureFlagDto);

        var flagData = new
        {
            Key = "bench-flag",
            IsEnabled = true,
            Rules = new[] { new { GroupId = 1, Attribute = "Email", Operator = "EndsWith", Value = "@gmail.com" } },
            RolloutPercentage = (int?)null
        };

        var dto = System.Text.Json.JsonSerializer.Deserialize(
            System.Text.Json.JsonSerializer.Serialize(flagData),
            dtoType,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        var cacheFlagMethod =
            typeof(ToggleMeshClient).GetMethod("CacheFlag", BindingFlags.NonPublic | BindingFlags.Instance);

        if (cacheFlagMethod == null)
            throw new Exception("Could not find CacheFlag method on ToggleMeshClient.");

        cacheFlagMethod.Invoke(_client, [dto!]);
    }

    [Benchmark]
    public bool IsEnabled_WithRules_Typed()
    {
        return _client.IsEnabled("bench-flag", _userContextTyped);
    }

    [Benchmark]
    public bool IsEnabled_WithRules_Dictionary()
    {
        return _client.IsEnabled("bench-flag", _userContextDictionary);
    }
}