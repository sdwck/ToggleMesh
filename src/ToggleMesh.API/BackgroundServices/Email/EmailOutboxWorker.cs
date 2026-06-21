using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Infrastructure.Email;
using ToggleMesh.API.Infrastructure.Email.Models;
using ToggleMesh.API.Persistence;

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

    public EmailOutboxWorker(IServiceProvider serviceProvider, ILogger<EmailOutboxWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOutboxAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing email outbox messages.");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task ProcessOutboxAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        var smtpSender = scope.ServiceProvider.GetRequiredService<SmtpEmailSender>();

        var messages = await db.EmailOutboxMessages
            .Where(m => m.Status == EmailOutboxStatus.Pending && m.NextAttemptAt <= DateTime.UtcNow)
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
                message.CompletedAt = DateTime.UtcNow;
                message.AttemptCount++;
            }
            catch (Exception ex)
            {
                message.AttemptCount++;
                message.ErrorMessage = ex.Message;

                if (message.AttemptCount >= MaxAttempts)
                {
                    message.Status = EmailOutboxStatus.Failed;
                    message.CompletedAt = DateTime.UtcNow;
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
                    
                    message.NextAttemptAt = DateTime.UtcNow.AddMinutes(minutes);
                }
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
