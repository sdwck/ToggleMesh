using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Email;
using ToggleMesh.API.Infrastructure.Email.Models;

namespace ToggleMesh.API.BackgroundServices.Email;

public class EmailOutboxWorker : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(15);
    private const int BatchSize = 20;
    private const int MaxAttempts = 5;

    private const int BackoffMinutesAttempt1 = 1;
    private const int BackoffMinutesAttempt2 = 5;
    private const int BackoffMinutesAttempt3 = 30;
    private const int BackoffMinutesDefault = 120;

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EmailOutboxWorker> _logger;
    private readonly TimeProvider _timeProvider;

    public EmailOutboxWorker(IServiceProvider serviceProvider, ILogger<EmailOutboxWorker> logger, TimeProvider timeProvider)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOutboxAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Error processing email outbox messages.");
            }

            await Task.Delay(PollInterval, _timeProvider, stoppingToken);
        }
    }

    private async Task ProcessOutboxAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        var smtpSender = scope.ServiceProvider.GetServices<IEmailSender>()
            .FirstOrDefault(s => s.GetType() != typeof(DatabaseOutboxEmailSender));

        if (smtpSender == null)
        {
            _logger.LogWarning("No direct IEmailSender configured (excluding DatabaseOutboxEmailSender). Outbox will not be processed.");
            return;
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var messages = await db.EmailOutboxMessages
            .Where(m => m.Status == EmailOutboxStatus.Pending && m.NextAttemptAt <= now)
            .OrderBy(m => m.NextAttemptAt)
            .Take(BatchSize)
            .ToListAsync(ct);

        if (messages.Count == 0)
            return;

        foreach (var message in messages)
        {
            try
            {
                await smtpSender.SendEmailAsync(message.ToEmail, message.Subject, message.HtmlBody, ct);

                message.Status = EmailOutboxStatus.Sent;
                message.CompletedAt = now;
                message.AttemptCount++;
            }
            catch (Exception ex)
            {
                message.AttemptCount++;
                message.ErrorMessage = ex.Message;

                if (message.AttemptCount >= MaxAttempts)
                {
                    message.Status = EmailOutboxStatus.Failed;
                    message.CompletedAt = now;
                }
                else
                {
                    var minutes = message.AttemptCount switch
                    {
                        1 => BackoffMinutesAttempt1,
                        2 => BackoffMinutesAttempt2,
                        3 => BackoffMinutesAttempt3,
                        _ => BackoffMinutesDefault
                    };
                    
                    message.NextAttemptAt = now.AddMinutes(minutes);
                }
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
