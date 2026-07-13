using Microsoft.EntityFrameworkCore;
using MathNet.Numerics.Distributions;
using ToggleMesh.API.Features.Webhooks.Domain;
using ToggleMesh.API.Infrastructure.Data;
using System.Threading.Channels;

namespace ToggleMesh.API.Features.Metrics.Workers;

public class SrmWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SrmWorker> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly Channel<WebhookEvent> _webhookChannel;

    public SrmWorker(
        IServiceProvider serviceProvider,
        ILogger<SrmWorker> logger,
        TimeProvider timeProvider,
        Channel<WebhookEvent> webhookChannel)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _timeProvider = timeProvider;
        _webhookChannel = webhookChannel;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SrmWorker started. Running every 1 minute.");

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1), _timeProvider);

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(45), _timeProvider, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                await DetectSrmAsync(stoppingToken);
                await timer.WaitForNextTickAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // ignore
        }
        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogCritical(ex, "Fatal error in SrmWorker");
        }
    }

    private async Task DetectSrmAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var activeExperiments = await db.FlagEnvironmentStates
                .Include(f => f.FeatureFlag)
                .Where(f => 
                    f.IsExperimentActive && 
                    !f.IsMabEnabled && !f.IsSrmAlertSent)
                .ToListAsync(ct);

            foreach (var exp in activeExperiments)
            {
                if (exp.FallthroughRollout.Count < 2)
                    continue;

                var totalWeight = exp.FallthroughRollout.Sum(r => r.Weight);
                if (totalWeight <= 0)
                    continue;

                var exposures = await db.ExperimentMetrics
                    .Where(m => m.EnvironmentId == exp.EnvironmentId && m.FlagKey == exp.FeatureFlag.Key && m.EventName == "$exposure")
                    .ToListAsync(ct);

                if (exposures.Count == 0)
                    continue;

                var totalExposures = exposures.Sum(e => e.TotalExposures);

                if (totalExposures < 1000)
                    continue;

                double chiSquareStat = 0;
                var validVariations = 0;

                foreach (var rollout in exp.FallthroughRollout)
                {
                    if (rollout.Weight <= 0) 
                        continue;

                    var observed = exposures.FirstOrDefault(e => e.VariationId == rollout.VariationId)?.TotalExposures ?? 0;
                    var expectedProb = (double)rollout.Weight / totalWeight;
                    var expected = totalExposures * expectedProb;

                    if (!(expected > 0)) 
                        continue;
                    
                    chiSquareStat += Math.Pow(observed - expected, 2) / expected;
                    validVariations++;
                }

                var dof = validVariations - 1;
                if (dof < 1) continue;

                var pValue = 1.0 - ChiSquared.CDF(dof, chiSquareStat);

                if (pValue >= 0.001) 
                    continue;
                
                _logger.LogWarning("SRM DETECTED: Flag {FlagKey} in env {EnvId}. ChiSquare: {ChiSquare}, p-value: {PValue}",
                    exp.FeatureFlag.Key, exp.EnvironmentId, chiSquareStat, pValue);

                exp.IsSrmAlertSent = true;
                exp.SrmPValue = pValue;

                var webhookEvent = new WebhookEvent(
                    exp.FeatureFlag.ProjectId,
                    exp.EnvironmentId,
                    "experiment.srm_detected",
                    exp.FeatureFlag.Key
                );

                _webhookChannel.Writer.TryWrite(webhookEvent);
            }

            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting SRM anomalies.");
        }
    }
}
