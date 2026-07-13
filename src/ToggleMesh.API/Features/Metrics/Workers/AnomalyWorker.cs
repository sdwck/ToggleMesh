using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Features.Analytics.Services;
using ToggleMesh.API.Features.Webhooks.Domain;
using ToggleMesh.API.Infrastructure.Data;

namespace ToggleMesh.API.Features.Metrics.Workers;

public class AnomalyWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AnomalyWorker> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly Channel<WebhookEvent> _webhookChannel;
    private readonly BayesianMathService _math;

    public AnomalyWorker(
        IServiceProvider serviceProvider,
        ILogger<AnomalyWorker> logger,
        TimeProvider timeProvider,
        Channel<WebhookEvent> webhookChannel,
        BayesianMathService math)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _timeProvider = timeProvider;
        _webhookChannel = webhookChannel;
        _math = math;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AnomalyWorker started. Running every 15 minutes.");

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(15), _timeProvider);

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(30), _timeProvider, stoppingToken);
            
            while (!stoppingToken.IsCancellationRequested)
            {
                await DetectAnomaliesAsync(stoppingToken);
                await timer.WaitForNextTickAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // ignore
        }
        catch (ObjectDisposedException)
        {
            // ignore
        }
        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogCritical(ex, "Fatal error in AnomalyWorker");
        }
    }

    private async Task DetectAnomaliesAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var activeExperiments = await db.FlagEnvironmentStates
                .Include(f => f.FeatureFlag)
                .Where(f => f.IsExperimentActive && f.MabGoalEvent != null && f.OffVariationId != null)
                .Select(f => new { f.EnvironmentId, f.FeatureFlag.Key, f.FeatureFlag.ProjectId, f.MabGoalEvent, f.OffVariationId })
                .ToListAsync(ct);

            if (activeExperiments.Count == 0) 
                return;

            foreach (var exp in activeExperiments)
            {
                var metrics = await db.ExperimentMetrics
                    .Where(m => m.EnvironmentId == exp.EnvironmentId 
                             && m.FlagKey == exp.Key 
                             && m.EventName == exp.MabGoalEvent)
                    .ToListAsync(ct);

                var control = metrics.FirstOrDefault(m => m.VariationId == exp.OffVariationId);
                var treatments = metrics.Where(m => m.VariationId != exp.OffVariationId).ToList();

                if (control == null || control.TotalExposures < 100) 
                    continue;

                foreach (var treatment in treatments)
                {
                    if (treatment.TotalExposures < 100) 
                        continue;
                    
                    if (treatment.IsAlertSent) 
                        continue;

                    var probBBeatsA = _math.CalculateProbabilityBBeatsA(
                        control.TotalExposures, control.TotalConversions,
                        treatment.TotalExposures, treatment.TotalConversions);

                    if (probBBeatsA < 0.05)
                    {
                        _logger.LogInformation("ANOMALY DETECTED: Variant for flag {FlagKey} in env {EnvId} is degrading. Prob: {Prob}", 
                            exp.Key, exp.EnvironmentId, probBBeatsA);

                        treatment.IsAlertSent = true;
                        
                        var webhookEvent = new WebhookEvent(
                            exp.ProjectId,
                            exp.EnvironmentId,
                            "experiment.degraded",
                            exp.Key
                        );

                        _webhookChannel.Writer.TryWrite(webhookEvent);
                    }
                    else if (probBBeatsA > 0.95)
                    {
                        _logger.LogInformation("WINNER FOUND: Variant for flag {FlagKey} in env {EnvId} is a winner. Prob: {Prob}", 
                            exp.Key, exp.EnvironmentId, probBBeatsA);

                        treatment.IsAlertSent = true;
                        
                        var webhookEvent = new WebhookEvent(
                            exp.ProjectId,
                            exp.EnvironmentId,
                            "experiment.winner_found",
                            exp.Key
                        );

                        _webhookChannel.Writer.TryWrite(webhookEvent);
                    }
                }
            }

            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking anomalies.");
        }
    }
}
