using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using ToggleMesh.SDK.Clients;
using ToggleMesh.SDK.Options;
using ToggleMesh.Common;
using ToggleMesh.Common.Rules;
using ToggleMesh.Common.Rules.Operators;
using Moq;
using Microsoft.Extensions.Logging;

namespace ToggleMesh.SDK.Tests;

public class UniversalSuiteTest
{
    private class FixtureData
    {
        public List<FixtureScenario> Scenarios { get; set; } = new();
    }

    private class FixtureScenario
    {
        public string Name { get; set; } = string.Empty;
        public List<FeatureFlagDto> Flags { get; set; } = [];
        public List<FixtureEvaluation> Evaluations { get; set; } = [];
    }

    private class FixtureEvaluation
    {
        public string FlagKey { get; set; } = string.Empty;
        public string Identity { get; set; } = string.Empty;
        public Dictionary<string, string> Context { get; set; } = new();
        public string ExpectedValue { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
    }

    [Fact]
    public void DebugSemVer()
    {
        var op = new SemVerEqualOperator();
        var compiled = op.Compile("1.2.3");
        Assert.NotNull(compiled);
        var result = op.Evaluate("v1.2.3", compiled);
        Assert.True(result, "Evaluate failed!");
    }

    [Fact]
    public void RunUniversalSuite()
    {
        var basePath = AppContext.BaseDirectory;
        var fixturePath = Path.Combine(basePath, "..", "..", "..", "..", "..", "..", "tests", "test-suite", "evaluation-fixtures.json");
        var json = File.ReadAllText(fixturePath);
            
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var data = JsonSerializer.Deserialize<FixtureData>(json, options);

        Assert.NotNull(data);
        Assert.NotEmpty(data.Scenarios);

        foreach (var scenario in data.Scenarios)
        {
            RunScenario(scenario);
        }
    }

    private void RunScenario(FixtureScenario scenario)
    {
        var sdkOptions = Microsoft.Extensions.Options.Options.Create(new ToggleMeshOptions
        {
            ApiKey = "test-key",
            BaseUrl = "http://localhost",
            UseFallbackFile = false
        });

        var logger = new Mock<ILogger<ToggleMeshClient>>();
        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(new HttpClient());
            
        var operators = new IRuleOperator[] 
        { 
            new EqualsOperator(),
            new NotEqualsOperator(),
            new ContainsOperator(),
            new StartsWithOperator(),
            new EndsWithOperator(),
            new RegexOperator(),
            new InListOperator(),
            new GreaterThanOperator(),
            new LessThanOperator(),
            new GreaterThanOrEqualOperator(),
            new LessThanOrEqualOperator(),
            new SemVerEqualOperator(),
            new SemVerGreaterThanOperator(),
            new SemVerGreaterThanOrEqualOperator(),
            new SemVerLessThanOperator(),
            new SemVerLessThanOrEqualOperator(),
            new DateBeforeOperator(),
            new DateAfterOperator()
        };

        var engine = new RuleEngine(operators);

        var client = new ToggleMeshClient(
            httpClientFactory.Object,
            sdkOptions,
            logger.Object,
            engine,
            []);

        var cacheField = typeof(ToggleMeshClient).GetField("_cache", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var cache = new ConcurrentDictionary<string, CachedFlag>();
            
        foreach (var flag in scenario.Flags)
        {
            var groups = engine.CompileRules(flag.Rules);
                
            Dictionary<string, object>? rolloutsTree = null;
            if (flag is { ContextualRollouts: not null, ContextPartitionKeys: not null })
            {
                rolloutsTree = new Dictionary<string, object>(StringComparer.Ordinal);
                foreach (var kvp in flag.ContextualRollouts)
                {
                    try
                    {
                        var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(kvp.Key);
                        if (dict == null) continue;
                            
                        var currentDict = rolloutsTree;
                        for (int i = 0; i < flag.ContextPartitionKeys.Length; i++)
                        {
                            var key = dict.GetValueOrDefault(flag.ContextPartitionKeys[i], "null");
                            if (i == flag.ContextPartitionKeys.Length - 1)
                            {
                                currentDict[key] = kvp.Value.ToArray();
                            }
                            else
                            {
                                if (!currentDict.TryGetValue(key, out var nextNode))
                                {
                                    nextNode = new Dictionary<string, object>(StringComparer.Ordinal);
                                    currentDict[key] = nextNode;
                                }
                                currentDict = (Dictionary<string, object>)nextNode;
                            }
                        }
                    }
                    catch
                    {
                        // ignored
                    }
                }
            }
            
            Guid? trueVarId = null;
            if (flag.Variations != null)
                foreach (var kvp in flag.Variations)
                    if (kvp.Value.Equals("true", StringComparison.OrdinalIgnoreCase))
                    {
                        trueVarId = kvp.Key;
                        break;
                    }
            
            var hasTargets = flag.IndividualTargets is { Count: > 0 };
            var strategy = groups.Length == 0 && rolloutsTree == null && !hasTargets 
                ? EvaluationStrategy.RolloutOnly 
                : EvaluationStrategy.Complex;

            var cachedFlag = new CachedFlag
            {
                Key = flag.Key,
                IsEnabled = flag.IsEnabled,
                OffVariationId = flag.OffVariationId,
                FallthroughRollout = flag.FallthroughRollout?.ToArray() ?? [],
                IndividualTargets = flag.IndividualTargets,
                ContextualRolloutsTree = rolloutsTree,
                ContextPartitionKeys = flag.ContextPartitionKeys, 
                HasContextualRollouts = rolloutsTree != null && flag.ContextPartitionKeys is { Length: > 0 },
                IsExperimentActive = flag.IsExperimentActive,
                Variations = flag.Variations,
                Groups = groups,
                OriginalDto = flag,
                Strategy = strategy,
                TrueVariationId = trueVarId
            };

            if (flag.Key == "op_flag")
            {
                foreach (var g in groups)
                {
                    foreach (var r in g.Rules)
                    {
                        if (r.Attribute == "attr_sveq")
                        {
                            Console.WriteLine($"[DEBUG] op_flag attr_sveq rule compiledValue: {r.CompiledValue} (type: {r.CompiledValue?.GetType()})");
                        }
                    }
                }
            }

            cache.TryAdd(flag.Key, cachedFlag);
        }

        cacheField!.SetValue(client, cache);

        foreach (var eval in scenario.Evaluations)
        {
            if (eval.Type == "string")
            {
                var result = client.GetStringVariation(eval.FlagKey, eval.Identity, eval.Context, "default");
                try
                {
                    Assert.Equal(eval.ExpectedValue, result);
                }
                catch (Exception ex)
                {
                    var contextStr = string.Join(", ", eval.Context.Select(kvp => $"{kvp.Key}={kvp.Value}"));
                    throw new Exception($"Failure in scenario '{scenario.Name}', flag '{eval.FlagKey}', Context: [{contextStr}]: Expected {eval.ExpectedValue}, got {result}", ex);
                }
            }
            else if (eval.Type == "boolean")
            {
                var result = client.IsEnabled(eval.FlagKey, eval.Identity, eval.Context, false);
                Assert.Equal(eval.ExpectedValue == "true", result);
            }
            else if (eval.Type == "number")
            {
                var result = client.GetJsonVariation<Dictionary<string, string>, double>(eval.FlagKey, eval.Identity, eval.Context, 0);
                var expectedNum = double.Parse(eval.ExpectedValue, CultureInfo.InvariantCulture);
                Assert.Equal(expectedNum, result);
            }
            else if (eval.Type == "json")
            {
                var result = client.GetJsonVariation<Dictionary<string, string>, JsonElement>(eval.FlagKey, eval.Identity, eval.Context, default);
                var expectedJson = JsonSerializer.Deserialize<JsonElement>(eval.ExpectedValue);
                Assert.Equal(expectedJson.GetRawText(), result.GetRawText());
            }
        }
    }
}