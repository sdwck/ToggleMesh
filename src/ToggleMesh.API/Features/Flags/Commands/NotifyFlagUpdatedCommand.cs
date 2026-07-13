using System.Text.Json;
using FastEndpoints;
using StackExchange.Redis;
using ToggleMesh.API.Features.Flags.Get;
using ToggleMesh.API.Infrastructure.Caching;
using ToggleMesh.API.Infrastructure.Streaming;
using ToggleMesh.Common;
using Polly;
using Polly.Retry;

namespace ToggleMesh.API.Features.Flags.Commands;

public record NotifyFlagUpdatedCommand(Guid EnvironmentId, string FlagKey, GetFlagResponse Response, FeatureFlagDto SdkDto) : ICommand;

public class NotifyFlagUpdatedCommandHandler : ICommandHandler<NotifyFlagUpdatedCommand>
{
    private readonly IDatabase _redis;
    private readonly ICacheInvalidator _cacheInvalidator;
    private readonly IToggleEventPublisher _publisher;
    private readonly ILogger<NotifyFlagUpdatedCommandHandler> _logger;
    private readonly IConfiguration _config;
    private readonly ResiliencePipeline _retryPipeline;

    public NotifyFlagUpdatedCommandHandler(
        IConnectionMultiplexer redis,
        ICacheInvalidator cacheInvalidator,
        IToggleEventPublisher publisher,
        ILogger<NotifyFlagUpdatedCommandHandler> logger,
        IConfiguration config)
    {
        _redis = redis.GetDatabase();
        _cacheInvalidator = cacheInvalidator;
        _publisher = publisher;
        _logger = logger;
        _config = config;
        
        _retryPipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 2,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromMilliseconds(100),
                OnRetry = args => 
                {
                    logger.LogWarning(args.Outcome.Exception, "Retry {Retry} failed", args.AttemptNumber + 1);
                    return default;
                }
            })
            .Build();
    }

    public async Task ExecuteAsync(NotifyFlagUpdatedCommand cmd, CancellationToken ct)
    {
        var cacheKey = CacheKeys.FlagState(cmd.EnvironmentId, cmd.FlagKey);
        var json = JsonSerializer.Serialize(cmd.Response);
        
        await _retryPipeline.ExecuteAsync(async token => 
        {
            var ttl = TimeSpan.FromMinutes(_config.GetValue("Caching:DefaultTtlMinutes", 10));
            await _redis.StringSetAsync(
                cacheKey, 
                json, 
                ttl);
                
            await _cacheInvalidator.InvalidateEnvironmentCacheAsync(cmd.EnvironmentId);
            
            await _publisher.PublishEventAsync(
                cmd.EnvironmentId.ToString(),
                "FlagUpdated",
                cmd.Response);
                
            await _publisher.PublishEventAsync(
                cmd.EnvironmentId.ToString(),
                "SdkFlagUpdated",
                cmd.SdkDto);
        }, ct);
    }
}
