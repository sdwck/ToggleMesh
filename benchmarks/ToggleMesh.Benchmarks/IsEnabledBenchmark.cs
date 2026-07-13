using System.Reflection;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.NoEmit;
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

public class BenchmarkConfig : ManualConfig
{
    public BenchmarkConfig()
    {
        AddColumn(StatisticColumn.P95);
        AddColumn(StatisticColumn.Max);
        AddColumn(StatisticColumn.Min);

        AddJob(Job.Default.WithToolchain(InProcessNoEmitToolchain.Instance));
    }
}

public class TypedUserContext
{
    public string Email { get; set; } = "test@gmail.com";
    public int Age { get; set; } = 25;
    public string AppVersion { get; set; } = "2.1.0";
}

[ToggleMeshContext]
public partial struct AotUserContext
{
    public string Email { get; set; }
    public int Age { get; set; }
    public string AppVersion { get; set; }
}


public struct PurchaseProps
{
    public double TotalAmount { get; set; }
    public int ItemsCount { get; set; }
}

[Config(typeof(BenchmarkConfig))]
[MemoryDiagnoser]
public class IsEnabledBenchmark
{
    private ToggleMeshClient _client = null!;
    private readonly TypedUserContext _userContextTyped = new();
    private AotUserContext _userContextAot = new() { Email = "test@gmail.com", Age = 25, AppVersion = "2.1.0" };
    private readonly Dictionary<string, string> _userContextDictionary = new() { { "Email", "test@gmail.com" }, { "Age", "25" }, { "AppVersion", "2.1.0" } };

    [GlobalSetup]
    public void Setup()
    {
        var operators = new IRuleOperator[] { 
            new EqualsOperator(), new EndsWithOperator(), new StartsWithOperator(), 
            new ContainsOperator(), new GreaterThanOperator(), new LessThanOperator(), 
            new SemVerEqualOperator(), new SemVerGreaterThanOperator(), 
            new SemVerLessThanOperator(), new NotEqualsOperator() 
        };
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
        
        var trueVarId = Guid.NewGuid();
        var falseVarId = Guid.NewGuid();
        var variations = new Dictionary<Guid, string>
        {
            { trueVarId, "true" },
            { falseVarId, "false" }
        };

        var flagData = new
        {
            Key = "bench-flag",
            IsEnabled = true,
            Rules = new[] { new { Priority = 0, GroupId = 1, Attribute = "Email", Operator = "EndsWith", Value = "@gmail.com" } },
            OffVariationId = falseVarId,
            FallthroughRollout = new[] { new { VariationId = trueVarId, Weight = 10000 } },
            Variations = variations
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
            Rules = new[] { new { Priority = 0, GroupId = 1, Attribute = "Email", Operator = "EndsWith", Value = "@gmail.com" } },
            OffVariationId = falseVarId,
            FallthroughRollout = new[] { new { VariationId = trueVarId, Weight = 10000 } },
            IsExperimentActive = true,
            ContextPartitionKeys = new[] { "Email" },
            ContextualRollouts = new Dictionary<string, object[]>
            {
                { "{\"Email\":\"test@gmail.com\"}", [new { VariationId = trueVarId, Weight = 10000 }] }
            },
            Variations = variations
        };

        var complexDto = System.Text.Json.JsonSerializer.Deserialize(
            System.Text.Json.JsonSerializer.Serialize(complexFlagData),
            dtoType,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        cacheFlagMethod.Invoke(_client, [complexDto!]);

        var flag10RulesData = new
        {
            Key = "bench-flag-10rules",
            IsEnabled = true,
            Rules = new[] { 
                new { Priority = 0, GroupId = 1, Attribute = "Email", Operator = "EndsWith", Value = "@gmail.com" },
                new { Priority = 0, GroupId = 1, Attribute = "Age", Operator = "GreaterThan", Value = "10" },
                new { Priority = 0, GroupId = 1, Attribute = "AppVersion", Operator = "SemVerGreaterThan", Value = "1.0.0" },
                new { Priority = 0, GroupId = 1, Attribute = "Email", Operator = "Equals", Value = "admin@gmail.com" },
                new { Priority = 0, GroupId = 2, Attribute = "Email", Operator = "Contains", Value = "test" },
                new { Priority = 0, GroupId = 2, Attribute = "AppVersion", Operator = "SemVerLessThan", Value = "3.0.0" },
                new { Priority = 0, GroupId = 2, Attribute = "Age", Operator = "LessThan", Value = "20" },
                new { Priority = 0, GroupId = 3, Attribute = "Email", Operator = "StartsWith", Value = "t" },
                new { Priority = 0, GroupId = 3, Attribute = "Age", Operator = "Equals", Value = "25" },
                new { Priority = 0, GroupId = 3, Attribute = "AppVersion", Operator = "SemVerEqual", Value = "2.1.0" }
            },
            OffVariationId = falseVarId,
            FallthroughRollout = new[] { new { VariationId = trueVarId, Weight = 10000 } },
            Variations = variations
        };

        var dto10Rules = System.Text.Json.JsonSerializer.Deserialize(
            System.Text.Json.JsonSerializer.Serialize(flag10RulesData),
            dtoType,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        cacheFlagMethod.Invoke(_client, [dto10Rules!]);

        var flagNoRulesData = new
        {
            Key = "bench-flag-norules",
            IsEnabled = true,
            Rules = Array.Empty<object>(),
            OffVariationId = falseVarId,
            FallthroughRollout = new[] { new { VariationId = trueVarId, Weight = 10000 } },
            Variations = variations
        };

        var dtoNoRules = System.Text.Json.JsonSerializer.Deserialize(
            System.Text.Json.JsonSerializer.Serialize(flagNoRulesData),
            dtoType,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        cacheFlagMethod.Invoke(_client, [dtoNoRules!]);

        var flagRolloutData = new
        {
            Key = "bench-flag-rollout",
            IsEnabled = true,
            Rules = Array.Empty<object>(),
            OffVariationId = falseVarId,
            FallthroughRollout = new[] { 
                new { VariationId = trueVarId, Weight = 5000 },
                new { VariationId = falseVarId, Weight = 5000 } 
            },
            Variations = variations
        };

        var dtoRollout = System.Text.Json.JsonSerializer.Deserialize(
            System.Text.Json.JsonSerializer.Serialize(flagRolloutData),
            dtoType,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        cacheFlagMethod.Invoke(_client, [dtoRollout!]);

        var stringVarId = Guid.NewGuid();
        var jsonVarId = Guid.NewGuid();
        var typedVariations = new Dictionary<Guid, string>
        {
            { stringVarId, "hello-world" },
            { jsonVarId, "{\"Theme\":\"light\",\"ShowSidebar\":false}" }
        };

        var stringFlagData = new
        {
            Key = "bench-flag-string",
            IsEnabled = true,
            OffVariationId = stringVarId,
            FallthroughRollout = new[] { new { VariationId = stringVarId, Weight = 10000 } },
            Variations = typedVariations
        };

        var jsonFlagData = new
        {
            Key = "bench-flag-json",
            IsEnabled = true,
            OffVariationId = jsonVarId,
            FallthroughRollout = new[] { new { VariationId = jsonVarId, Weight = 10000 } },
            Variations = typedVariations
        };

        var stringDto = System.Text.Json.JsonSerializer.Deserialize(
            System.Text.Json.JsonSerializer.Serialize(stringFlagData),
            dtoType,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        var jsonDto = System.Text.Json.JsonSerializer.Deserialize(
            System.Text.Json.JsonSerializer.Serialize(jsonFlagData),
            dtoType,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        cacheFlagMethod.Invoke(_client, [stringDto!]);
        cacheFlagMethod.Invoke(_client, [jsonDto!]);
    }

    [Benchmark]
    [BenchmarkCategory("Bool")]
    public bool Evaluate_1Rule_TypedContext()
    {
        return _client.IsEnabled("bench-flag", _userContextTyped);
    }

    [Benchmark]
    [BenchmarkCategory("Bool")]
    public bool Evaluate_1Rule_AOT()
    {
        return _client.IsEnabled("bench-flag", ref _userContextAot);
    }

    [Benchmark]
    [BenchmarkCategory("Bool")]
    public bool Evaluate_1Rule_Dictionary()
    {
        return _client.IsEnabled("bench-flag", _userContextDictionary);
    }

    [Benchmark]
    [BenchmarkCategory("Bool", "Complex")]
    public bool Evaluate_ComplexRule_TypedContext()
    {
        return _client.IsEnabled("bench-flag-complex", _userContextTyped);
    }

    [Benchmark]
    [BenchmarkCategory("Bool", "Complex")]
    public bool Evaluate_ComplexRule_AOT()
    {
        return _client.IsEnabled("bench-flag-complex", ref _userContextAot);
    }

    [Benchmark]
    [BenchmarkCategory("Bool", "Complex")]
    public bool Evaluate_ComplexRule_Dictionary()
    {
        return _client.IsEnabled("bench-flag-complex", _userContextDictionary);
    }

    private readonly PurchaseProps _purchaseProps = new() { TotalAmount = 99.99, ItemsCount = 1 };

    [Benchmark]
    [BenchmarkCategory("Track")]
    public void Analytics_TrackEvent_Simple()
    {
        _client.Track("purchase", "user-123", _purchaseProps, value: 99.99);
    }

    [Benchmark]
    [BenchmarkCategory("Bool", "Complex")]
    public bool Evaluate_10Rules_AOT()
    {
        return _client.IsEnabled("bench-flag-10rules", ref _userContextAot);
    }

    [Benchmark]
    [BenchmarkCategory("Track")]
    public void Analytics_TrackEvent_10Rules_AOT()
    {
        _client.Track("purchase", ref _userContextAot, _purchaseProps, value: 99.99);
    }

    [Benchmark]
    [BenchmarkCategory("Bool", "NoRules")]
    public bool Evaluate_NoRules_AOT()
    {
        return _client.IsEnabled("bench-flag-norules", ref _userContextAot);
    }

    [Benchmark]
    [BenchmarkCategory("Bool", "Rollout")]
    public bool Evaluate_50_50_Rollout_AOT()
    {
        return _client.IsEnabled("bench-flag-rollout", ref _userContextAot);
    }

    [Benchmark]
    [BenchmarkCategory("String")]
    public string GetStringVariation()
    {
        return _client.GetStringVariation("bench-flag-string", "default-value");
    }

    private readonly FeatureSettings _defaultSettings = new();

    [Benchmark]
    [BenchmarkCategory("Json")]
    public FeatureSettings GetJsonVariation()
    {
        return _client.GetJsonVariation("bench-flag-json", _defaultSettings);
    }
}

public class FeatureSettings
{
    public string Theme { get; set; } = "dark";
    public bool ShowSidebar { get; set; } = true;
}

