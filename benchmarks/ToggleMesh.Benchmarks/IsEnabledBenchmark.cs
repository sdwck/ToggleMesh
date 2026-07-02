using System.Reflection;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using ToggleMesh.Common;
using ToggleMesh.Common.Rules;
using ToggleMesh.Common.Rules.Operators;
using ToggleMesh.SDK.Attributes;
using ToggleMesh.SDK.Clients;
using ToggleMesh.SDK.Options;

namespace ToggleMesh.Benchmarks;

public class TypedUserContext
{
    public string Email { get; set; } = "test@gmail.com";
    public int Age { get; set; } = 25;
}

[ToggleMeshContext]
public partial struct AotUserContext
{
    public string Email { get; set; }
    public int Age { get; set; }
}


public struct PurchaseProps
{
    public double TotalAmount { get; set; }
    public int ItemsCount { get; set; }
}

[MemoryDiagnoser]
public class IsEnabledBenchmark
{
    private ToggleMeshClient _client = null!;
    private readonly TypedUserContext _userContextTyped = new();
    private AotUserContext _userContextAot = new() { Email = "test@gmail.com", Age = 25 };
    private readonly Dictionary<string, string> _userContextDictionary = new() { { "Email", "test@gmail.com" }, { "Age", "25" } };

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

        var complexFlagData = new
        {
            Key = "bench-flag-complex",
            IsEnabled = true,
            Rules = new[] { new { GroupId = 1, Attribute = "Email", Operator = "EndsWith", Value = "@gmail.com" } },
            RolloutPercentage = 100,
            IsExperimentActive = true,
            ContextPartitionKeys = new[] { "Email", "Age" },
            ContextualRollouts = new Dictionary<string, int>
            {
                { "{\"Email\":\"test@gmail.com\",\"Age\":\"25\"}", 100 }
            }
        };

        var complexDto = System.Text.Json.JsonSerializer.Deserialize(
            System.Text.Json.JsonSerializer.Serialize(complexFlagData),
            dtoType,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        cacheFlagMethod.Invoke(_client, [complexDto!]);
    }

    [Benchmark]
    public bool IsEnabled_WithRules_Typed()
    {
        return _client.IsEnabled("bench-flag", _userContextTyped);
    }

    [Benchmark]
    public bool IsEnabled_WithRules_AOT()
    {
        return _client.IsEnabled("bench-flag", ref _userContextAot);
    }

    [Benchmark]
    public bool IsEnabled_WithRules_Dictionary()
    {
        return _client.IsEnabled("bench-flag", _userContextDictionary);
    }

    [Benchmark]
    public bool IsEnabled_Complex_Typed()
    {
        return _client.IsEnabled("bench-flag-complex", "user-123", _userContextTyped);
    }

    [Benchmark]
    public bool IsEnabled_Complex_AOT()
    {
        return _client.IsEnabled("bench-flag-complex", "user-123", ref _userContextAot);
    }

    [Benchmark]
    public bool IsEnabled_Complex_Dictionary()
    {
        return _client.IsEnabled("bench-flag-complex", "user-123", _userContextDictionary);
    }

    private readonly PurchaseProps _purchaseProps = new() { TotalAmount = 99.99, ItemsCount = 1 };

    [Benchmark]
    public void Track_Event()
    {
        _client.Track("purchase", "user-123", _purchaseProps, value: 99.99);
    }
}