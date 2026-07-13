using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using ToggleMesh.API.Features.Analytics.Domain;
using ToggleMesh.API.Features.Analytics.Ingest;
using ToggleMesh.API.Features.Analytics.Services;
using ToggleMesh.API.Features.Flags.Commands;
using ToggleMesh.API.Features.Flags.Domain;
using ToggleMesh.API.Features.Flags.Experiments.Start;
using ToggleMesh.API.Features.Flags.Get;
using ToggleMesh.API.Features.Projects.Domain;
using ToggleMesh.API.Infrastructure.Caching;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Security;
using ToggleMesh.API.Infrastructure.Streaming;
using ToggleMesh.IntegrationTests.Infrastructure;

namespace ToggleMesh.IntegrationTests.Flags;

[Collection("SharedEnv3")]
public class ExperimentLifecycleTests : IAsyncLifetime
{
    public async Task InitializeAsync() => await _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;

    public ExperimentLifecycleTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<(Guid ProjectId, Guid EnvironmentId, string ApiKey, string FlagKey)> SeedExperimentScenarioAsync(
        string flagKey,
        bool isExperimentActive = false,
        bool isMabEnabled = false,
        string? mabGoalEvent = null,
        ICollection<ContextualRollout>? contextualRollouts = null,
        int? rolloutPercentage = 50)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var project = new Project { Name = "Lifecycle Test Project" };
        db.Projects.Add(project);
        db.ProjectMembers.Add(new ProjectMember
        {
            Project = project,
            UserId = Guid.Parse(TestAuthHandler.TestUserId),
            Role = ProjectRole.Owner
        });

        var environment = new ProjectEnvironment { Name = "Production", Project = project };
        db.Environments.Add(environment);

        var plainKey = Guid.NewGuid().ToString("N");
        var keyHash = ApiKeyHasher.Hash(plainKey);
        var key = new EnvironmentKey
        {
            Environment = environment,
            KeyHash = keyHash,
            KeyPreview = ApiKeyHasher.GeneratePreview(keyHash),
            CreatedOn = DateTime.UtcNow
        };
        db.EnvironmentKeys.Add(key);


        var flag = new FeatureFlag
        {
            Project = project,
            Key = flagKey
        };
        db.FeatureFlags.Add(flag);

        var state = new FlagEnvironmentState
        {
            Environment = environment,
            FeatureFlag = flag,
            IsEnabled = true,
            IsExperimentActive = isExperimentActive,
            IsMabEnabled = isMabEnabled,
            MabGoalEvent = mabGoalEvent,
            ContextualRollouts = contextualRollouts,
            FallthroughRollout = rolloutPercentage.HasValue ? new List<VariationWeight> { new() { VariationId = Guid.Empty, Weight = rolloutPercentage.Value * 100 } } : null,
            ExperimentStartedAt = isExperimentActive ? DateTimeOffset.UtcNow : null
        };
        db.FlagEnvironmentStates.Add(state);

        await db.SaveChangesAsync();
        return (project.Id, environment.Id, plainKey, flag.Key);
    }

    private async Task InvokeRollupWorkerPrivateMethodsAsync(
        AppDbContext db,
        BayesianMathService math,
        IToggleEventPublisher hubContext,
        IDatabase redis,
        CancellationToken ct)
    {
        var scope = _factory.Services.CreateScope();
        var mabShifter = scope.ServiceProvider.GetRequiredService<IMabTrafficShifterService>();
        var notifyHandler = new NotifyFlagUpdatedCommandHandler(
            scope.ServiceProvider.GetRequiredService<IConnectionMultiplexer>(),
            scope.ServiceProvider.GetRequiredService<ICacheInvalidator>(),
            hubContext,
            scope.ServiceProvider.GetRequiredService<ILogger<NotifyFlagUpdatedCommandHandler>>(),
            scope.ServiceProvider.GetRequiredService<IConfiguration>()
        );

        await mabShifter.ProcessMabTrafficShiftingAsync(db, math, notifyHandler, ct);
        await mabShifter.ProcessContextualBanditAutoSegmentationAsync(db, math, notifyHandler, ct);
    }

    [Fact]
    public async Task StartExperiment_WhenAlreadyActive_ShouldReturn400()
    {
        // Arrange
        var flagKey = "already_active_flag";
        var (projectId, envId, _, _) = await SeedExperimentScenarioAsync(
            flagKey: flagKey,
            isExperimentActive: true);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.ExperimentMetrics.Add(new ExperimentMetric
            {
                EnvironmentId = envId,
                FlagKey = flagKey,
                EventName = "click",
                VariationId = Guid.Empty,
                TotalExposures = 10,
                TotalConversions = 1,
                LastCalculatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        // Act
        var request = new StartExperimentRequest
        {
            Mode = "classic",
            GoalEvent = "click"
        };
        var response = await _client.PostAsJsonAsync(
            $"/api/v1/projects/{projectId}/environments/{envId}/flags/{flagKey}/experiments/start", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var metrics = await db.ExperimentMetrics
                .Where(x => x.EnvironmentId == envId && x.FlagKey == flagKey)
                .ToListAsync();
            metrics.Should().NotBeEmpty("existing metrics should not be cleared when starting an already active experiment");
        }
    }

    [Fact]
    public async Task StopExperiment_WhenNotActive_ShouldReturn400()
    {
        // Arrange
        var flagKey = "inactive_flag";
        var (projectId, envId, _, _) = await SeedExperimentScenarioAsync(
            flagKey: flagKey,
            isExperimentActive: false);

        // Act
        var response = await _client.PostAsJsonAsync<object?>(
            $"/api/v1/projects/{projectId}/environments/{envId}/flags/{flagKey}/experiments/stop", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var iterations = await db.ExperimentIterations
                .Where(x => x.EnvironmentId == envId && x.FlagKey == flagKey)
                .ToListAsync();
            iterations.Should().BeEmpty("no new iterations should be created when stopping a not active experiment");
        }
    }

    [Fact]
    public async Task StopExperiment_ShouldFreezeMab_AndCreateIterationSnapshot_WithCorrectData()
    {
        // Arrange
        var contextRollouts = new List<ContextualRollout> { new ContextualRollout { Id = Guid.NewGuid(), ContextSlice = "{\"Country\":\"US\"}", Rollout = new List<ToggleMesh.API.Features.Flags.Domain.VariationWeight> { new ToggleMesh.API.Features.Flags.Domain.VariationWeight { VariationId = Guid.Empty, Weight = 82 * 100 } } } };
        var (projectId, envId, _, flagKey) = await SeedExperimentScenarioAsync(
            flagKey: "mab_freeze_flag",
            isExperimentActive: true,
            isMabEnabled: true,
            mabGoalEvent: "video_played",
            contextualRollouts: contextRollouts,
            rolloutPercentage: 82);
        
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.ExperimentMetrics.AddRange(
                new ExperimentMetric
                {
                    EnvironmentId = envId,
                    FlagKey = flagKey,
                    EventName = "video_played",
                    VariationId = Guid.Empty,
                    TotalExposures = 100,
                    TotalConversions = 10,
                    LastCalculatedAt = DateTimeOffset.UtcNow
                },
                new ExperimentMetric
                {
                    EnvironmentId = envId,
                    FlagKey = flagKey,
                    EventName = "video_played",
                    VariationId = Guid.NewGuid(),
                    TotalExposures = 99,
                    TotalConversions = 20,
                    LastCalculatedAt = DateTimeOffset.UtcNow
                }
            );

            db.ContextualExperimentMetrics.AddRange(
                new ContextualExperimentMetric
                {
                    EnvironmentId = envId,
                    FlagKey = flagKey,
                    EventName = "video_played",
                    VariationId = Guid.Empty,
                    ContextSlice = "{\"Country\":\"US\"}",
                    TotalExposures = 50,
                    TotalConversions = 5,
                    LastCalculatedAt = DateTimeOffset.UtcNow
                },
                new ContextualExperimentMetric
                {
                    EnvironmentId = envId,
                    FlagKey = flagKey,
                    EventName = "video_played",
                    VariationId = Guid.NewGuid(),
                    ContextSlice = "{\"Country\":\"US\"}",
                    TotalExposures = 49,
                    TotalConversions = 12,
                    LastCalculatedAt = DateTimeOffset.UtcNow
                }
            );
            await db.SaveChangesAsync();
        }

        // Act
        var response = await _client.PostAsJsonAsync<object?>(
            $"/api/v1/projects/{projectId}/environments/{envId}/flags/{flagKey}/experiments/stop", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var state = await db.FlagEnvironmentStates
                .FirstAsync(x => x.EnvironmentId == envId && x.FeatureFlag.Key == flagKey);
            
            state.IsExperimentActive.Should().BeFalse("experiment should no longer be active");
            state.IsMabEnabled.Should().BeFalse("MAB auto-tune should be frozen/disabled");

            var iterations = await db.ExperimentIterations
                .Where(x => x.EnvironmentId == envId && x.FlagKey == flagKey)
                .ToListAsync();

            iterations.Should().ContainSingle("exactly one iteration snapshot should be created");
            var iteration = iterations.Single();

            var snapshot = JsonSerializer.Deserialize<SnapshotContainer>(
                iteration.FinalMetricsSnapshot,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            snapshot.Should().NotBeNull();

            snapshot.Global.Should().ContainSingle(x => x.EventName == "video_played");
            var globalMetric = snapshot.Global.Single(x => x.EventName == "video_played");
            globalMetric.Variations.Should().HaveCount(2);
            var gControl = globalMetric.Variations.First(v => v.VariationId == Guid.Empty);
            var gTreatment = globalMetric.Variations.First(v => v.VariationId != Guid.Empty);
            gControl.Exposures.Should().Be(100);
            gControl.Conversions.Should().Be(10);
            gTreatment.Exposures.Should().Be(99);
            gTreatment.Conversions.Should().Be(20);
            gTreatment.ExpectedUplift.Should().NotBe(0);
            gTreatment.ProbabilityToBeatBaseline.Should().NotBe(0);

            snapshot.Contextual.Should().ContainSingle(x => x.EventName == "video_played" && x.ContextSlice == "{\"Country\":\"US\"}");
            var contextualMetric = snapshot.Contextual.Single(x => x.EventName == "video_played" && x.ContextSlice == "{\"Country\":\"US\"}");
            contextualMetric.Variations.Should().HaveCount(2);
            var cControl = contextualMetric.Variations.First(v => v.VariationId == Guid.Empty);
            var cTreatment = contextualMetric.Variations.First(v => v.VariationId != Guid.Empty);
            cControl.Exposures.Should().Be(50);
            cControl.Conversions.Should().Be(5);
            cTreatment.Exposures.Should().Be(49);
            cTreatment.Conversions.Should().Be(12);
            cTreatment.ExpectedUplift.Should().NotBe(0);
            cTreatment.ProbabilityToBeatBaseline.Should().NotBe(0);
        }
    }

    [Fact]
    public async Task RestartExperiment_ShouldWipeOldMetrics_AndClearContextualRollouts()
    {
        // Arrange
        var flagKey = "restart_wipe_flag";
        var (projectId, envId, plainKey, _) = await SeedExperimentScenarioAsync(
            flagKey: flagKey,
            isExperimentActive: false,
            isMabEnabled: false,
            contextualRollouts: new List<ContextualRollout> { new ContextualRollout { Id = Guid.NewGuid(), ContextSlice = "{\"Country\":\"US\"}", Rollout = new List<ToggleMesh.API.Features.Flags.Domain.VariationWeight> { new ToggleMesh.API.Features.Flags.Domain.VariationWeight { VariationId = Guid.Empty, Weight = 50 * 100 } } } },
            rolloutPercentage: 50);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.ExperimentMetrics.Add(new ExperimentMetric
            {
                EnvironmentId = envId,
                FlagKey = flagKey,
                EventName = "click",
                VariationId = Guid.Empty,
                TotalExposures = 50,
                TotalConversions = 5,
                LastCalculatedAt = DateTimeOffset.UtcNow
            });
            db.ContextualExperimentMetrics.Add(new ContextualExperimentMetric
            {
                EnvironmentId = envId,
                FlagKey = flagKey,
                EventName = "click",
                VariationId = Guid.Empty,
                ContextSlice = "{\"Country\":\"US\"}",
                TotalExposures = 25,
                TotalConversions = 2,
                LastCalculatedAt = DateTimeOffset.UtcNow
            });
            db.AnalyticsExposures.Add(new AnalyticsExposure
            {
                EnvironmentId = envId,
                FlagKey = flagKey,
                Identity = "user_1",
                VariationId = Guid.Empty,
                Timestamp = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        // Act
        var request = new StartExperimentRequest
        {
            Mode = "classic",
            GoalEvent = "click"
        };
        var response = await _client.PostAsJsonAsync(
            $"/api/v1/projects/{projectId}/environments/{envId}/flags/{flagKey}/experiments/start", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var metricsCount = await db.ExperimentMetrics
                .CountAsync(x => x.EnvironmentId == envId && x.FlagKey == flagKey);
            metricsCount.Should().Be(0, "global metrics should be cleared");

            var contextualMetricsCount = await db.ContextualExperimentMetrics
                .CountAsync(x => x.EnvironmentId == envId && x.FlagKey == flagKey);
            contextualMetricsCount.Should().Be(0, "contextual metrics should be cleared");

            var exposuresCount = await db.AnalyticsExposures
                .CountAsync(x => x.EnvironmentId == envId && x.FlagKey == flagKey);
            exposuresCount.Should().Be(0, "analytics exposures should be cleared");

            var state = await db.FlagEnvironmentStates
                .FirstAsync(x => x.EnvironmentId == envId && x.FeatureFlag.Key == flagKey);
            state.ContextualRollouts.Should().BeNullOrEmpty("contextual rollouts should be reset to null/empty");
        }
    }

    [Fact]
    public async Task RollupWorker_ShouldIgnoreStoppedExperiments()
    {
        // Arrange
        var flagKey = "stopped_worker_flag";
        var contextRollouts = new List<ContextualRollout> { new ContextualRollout { Id = Guid.NewGuid(), ContextSlice = "{\"Country\":\"US\"}", Rollout = new List<ToggleMesh.API.Features.Flags.Domain.VariationWeight> { new ToggleMesh.API.Features.Flags.Domain.VariationWeight { VariationId = Guid.Empty, Weight = 50 * 100 } } } };
        var (projectId, envId, _, _) = await SeedExperimentScenarioAsync(
            flagKey: flagKey,
            isExperimentActive: false,
            isMabEnabled: true,
            mabGoalEvent: "video_played",
            contextualRollouts: contextRollouts,
            rolloutPercentage: 50);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.ExperimentMetrics.AddRange(
                new ExperimentMetric
                {
                    EnvironmentId = envId,
                    FlagKey = flagKey,
                    EventName = "video_played",
                    VariationId = Guid.Empty,
                    TotalExposures = 100,
                    TotalConversions = 10,
                    LastCalculatedAt = DateTimeOffset.UtcNow
                },
                new ExperimentMetric
                {
                    EnvironmentId = envId,
                    FlagKey = flagKey,
                    EventName = "video_played",
                    VariationId = Guid.NewGuid(),
                    TotalExposures = 100,
                    TotalConversions = 95,
                    LastCalculatedAt = DateTimeOffset.UtcNow
                }
            );

            db.ContextualExperimentMetrics.AddRange(
                new ContextualExperimentMetric
                {
                    EnvironmentId = envId,
                    FlagKey = flagKey,
                    EventName = "video_played",
                    VariationId = Guid.Empty,
                    ContextSlice = "{\"Country\":\"US\"}",
                    TotalExposures = 100,
                    TotalConversions = 10,
                    LastCalculatedAt = DateTimeOffset.UtcNow
                },
                new ContextualExperimentMetric
                {
                    EnvironmentId = envId,
                    FlagKey = flagKey,
                    EventName = "video_played",
                    VariationId = Guid.NewGuid(),
                    ContextSlice = "{\"Country\":\"US\"}",
                    TotalExposures = 100,
                    TotalConversions = 95,
                    LastCalculatedAt = DateTimeOffset.UtcNow
                }
            );
            await db.SaveChangesAsync();
        }

        // Act
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var math = scope.ServiceProvider.GetRequiredService<BayesianMathService>();
            var hubContext = scope.ServiceProvider.GetRequiredService<IToggleEventPublisher>();
            var redis = scope.ServiceProvider.GetRequiredService<IConnectionMultiplexer>().GetDatabase();

            await InvokeRollupWorkerPrivateMethodsAsync(db, math, hubContext, redis, CancellationToken.None);
        }

        // Assert
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var state = await db.FlagEnvironmentStates
                .Include(x => x.ContextualRollouts)
                .FirstAsync(x => x.EnvironmentId == envId && x.FeatureFlag.Key == flagKey);

            (state.FallthroughRollout!.First().Weight / 100).Should().Be(50, "rollout percentage should remain unchanged for stopped experiment");
            state.ContextualRollouts.Should().NotBeNull();
            (state.ContextualRollouts!.FirstOrDefault(c => c.ContextSlice == "{\"Country\":\"US\"}")?.Rollout!.First().Weight).Should().Be(5000, "contextual rollouts should remain unchanged for stopped experiment");
        }
    }

    [Fact]
    public async Task StartExperiment_ShouldBroadcast_CleanState_ViaSignalR()
    {
        // Arrange
        var flagKey = "signalr_broadcast_flag";
        var (projectId, envId, apiKey, _) = await SeedExperimentScenarioAsync(
            flagKey: flagKey,
            isExperimentActive: false);

        var tcs = new TaskCompletionSource<GetFlagResponse>();

        var sseClient = _factory.CreateClient();
        sseClient.DefaultRequestHeaders.Add("x-api-key", apiKey);
        var cts = new CancellationTokenSource();
        _ = Task.Run(async () =>
        {
            try
            {
                var req = new HttpRequestMessage(HttpMethod.Get, "/api/v1/stream");
                req.Headers.Add("Accept", "text/event-stream");
                var resp = await sseClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cts.Token);
                var stream = await resp.Content.ReadAsStreamAsync(cts.Token);
                using var reader = new StreamReader(stream);
                while (!cts.IsCancellationRequested)
                {
                    var line = await reader.ReadLineAsync(cts.Token);
                    if (line?.StartsWith("data: ") == true)
                    {
                        var data = line.Substring(6);
                        var doc = JsonDocument.Parse(data);
                        if (doc.RootElement.TryGetProperty("EventName", out var evtName) && evtName.GetString() == "FlagUpdated")
                        {
                            if (doc.RootElement.TryGetProperty("Payload", out var payload))
                            {
                                var flag = JsonSerializer.Deserialize<GetFlagResponse>(payload.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                                if (flag != null)
                                {
                                    tcs.TrySetResult(flag);
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            catch { /* ignore */ }
        }, cts.Token);

        await Task.Delay(500, cts.Token);

        // Act
        var request = new StartExperimentRequest
        {
            Mode = "classic",
            GoalEvent = "video_played"
        };
        var response = await _client.PostAsJsonAsync(
            $"/api/v1/projects/{projectId}/environments/{envId}/flags/{flagKey}/experiments/start", request, cancellationToken: cts.Token);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert
        var signalRTask = tcs.Task;
        var completedTask = await Task.WhenAny(signalRTask, Task.Delay(10000, cts.Token));

        completedTask.Should().Be(signalRTask, "SignalR event was not received within 10 seconds");

        var receivedFlag = await signalRTask;
        receivedFlag.Key.Should().Be(flagKey);
        receivedFlag.IsExperimentActive.Should().BeTrue("IsExperimentActive should be true in SignalR broadcast");
        receivedFlag.TrueCount.Should().Be(0L, "TrueCount should be reset to 0 in SignalR broadcast");
        receivedFlag.FalseCount.Should().Be(0L, "FalseCount should be reset to 0 in SignalR broadcast");

        await cts.CancelAsync();
    }

    private record SnapshotContainer(
        List<ExperimentResultDto> Global,
        List<ContextualExperimentResultDto> Contextual);

    private record ExperimentResultDto(
        string EventName,
        List<VariationResultDto> Variations);

    private record VariationResultDto(
        Guid VariationId,
        long Exposures,
        long Conversions,
        double ExpectedUplift,
        double ProbabilityToBeatBaseline);

    private record ContextualExperimentResultDto(
        string ContextSlice,
        string EventName,
        List<VariationResultDto> Variations,
        int? CurrentRollout);
}
