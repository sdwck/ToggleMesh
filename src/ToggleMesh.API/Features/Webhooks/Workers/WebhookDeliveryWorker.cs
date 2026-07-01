using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Features.Webhooks;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Security;

namespace ToggleMesh.API.BackgroundServices.Webhooks;

public class WebhookDeliveryWorker : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(15);
    private const int BatchSize = 50;
    private const int MaxConsecutiveFailures = 10;
    private const int MaxAttempts = 5;

    private const int BackoffMinutesAttempt1 = 1;
    private const int BackoffMinutesAttempt2 = 5;
    private const int BackoffMinutesAttempt3 = 30;
    private const int BackoffMinutesDefault = 120;

    private readonly IServiceProvider _serviceProvider;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WebhookDeliveryWorker> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly IAesEncryptionService _encryptionService;

    public WebhookDeliveryWorker(
        IServiceProvider serviceProvider,
        IHttpClientFactory httpClientFactory,
        ILogger<WebhookDeliveryWorker> logger,
        TimeProvider timeProvider,
        IAesEncryptionService encryptionService)
    {
        _serviceProvider = serviceProvider;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _timeProvider = timeProvider;
        _encryptionService = encryptionService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessDeliveriesAsync(stoppingToken);
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
                _logger.LogError(ex, "Error processing webhook deliveries.");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task ProcessDeliveriesAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var deliveries = await db.WebhookDeliveries
            .Include(d => d.Webhook)
            .Where(d => 
                d.Status == WebhookDeliveryStatus.Pending && 
                d.NextAttemptAt <= _timeProvider.GetUtcNow().UtcDateTime)
            .OrderBy(d => d.NextAttemptAt)
            .Take(BatchSize)
            .ToListAsync(ct);

        if (deliveries.Count == 0) 
            return;

        var client = _httpClientFactory.CreateClient("WebhookClient");

        var tasks = deliveries.Select(async delivery =>
        {
            if (delivery.Webhook.Status != WebhookStatus.Active)
                return new
                {
                    Delivery = delivery, 
                     Success = false, 
                     Aborted = true, 
                     Error = (string?)null, 
                     StatusCode = (int?)null
                };

            try
            {
                if (!await SsrfValidator.IsSafeUrlAsync(delivery.Webhook.Url, ct))
                    return new
                    {
                        Delivery = delivery, 
                        Success = false, 
                        Aborted = true, 
                        Error = "SSRF Validation Failed: Private or local IP addresses are not allowed.", 
                        StatusCode = (int?)null
                    }!;

                var payloadBytes = Encoding.UTF8.GetBytes(delivery.Payload);
                
                var rawSecret = delivery.Webhook.SecretKey;

                var decryptedSecret = rawSecret.StartsWith("v1:") 
                    ? _encryptionService.Decrypt(rawSecret[3..]) 
                    : rawSecret;
                
                var secretBytes = Encoding.UTF8.GetBytes(decryptedSecret);

                using var hmac = new HMACSHA256(secretBytes);
                var hash = hmac.ComputeHash(payloadBytes);
                var signature = "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();

                var req = new HttpRequestMessage(HttpMethod.Post, new Uri(delivery.Webhook.Url))
                {
                    Content = new StringContent(
                        delivery.Payload, Encoding.UTF8, "application/json")
                };
                req.Headers.Add("x-togglemesh-signature", signature);

                var resp = await client.SendAsync(
                    req, HttpCompletionOption.ResponseHeadersRead, ct);
                
                if (resp.IsSuccessStatusCode)
                    return new
                    {
                        Delivery = delivery, 
                        Success = true, 
                        Aborted = false, 
                        Error = (string?)null, 
                        StatusCode = (int?)resp.StatusCode
                    };

                return new
                {
                    Delivery = delivery, 
                    Success = false, 
                    Aborted = false, 
                    Error = $"HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}", 
                    StatusCode = (int?)resp.StatusCode
                };
            }
            catch (Exception ex)
            {
                return new 
                { 
                    Delivery = delivery, 
                    Success = false, 
                    Aborted = false, 
                    Error = ex.Message, 
                    StatusCode = (int?)null }!;
            }
        });

        var results = await Task.WhenAll(tasks);

        foreach (var result in results)
        {
            var delivery = result.Delivery;
            if (result.Aborted)
            {
                delivery.Status = WebhookDeliveryStatus.Failed;
                delivery.ErrorMessage = $"Delivery aborted. Webhook status is {delivery.Webhook.Status}.";
                delivery.CompletedAt = _timeProvider.GetUtcNow().UtcDateTime;
                continue;
            }

            delivery.AttemptCount++;
            delivery.StatusCode = result.StatusCode;

            if (result.Success)
            {
                delivery.Status = WebhookDeliveryStatus.Success;
                delivery.CompletedAt = _timeProvider.GetUtcNow().UtcDateTime;
                
                delivery.Webhook.ConsecutiveFailures = 0;
                delivery.Webhook.LastTriggeredAt = _timeProvider.GetUtcNow().UtcDateTime;
            }
            else
            {
                delivery.ErrorMessage = result.Error;
                HandleFailure(delivery);
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private void HandleFailure(WebhookDelivery delivery)
    {
        delivery.Webhook.ConsecutiveFailures++;
        
        if (delivery.Webhook.ConsecutiveFailures >= MaxConsecutiveFailures)
        {
            delivery.Webhook.Status = WebhookStatus.DisabledBySystem;
            _logger.LogWarning("Webhook {WebhookId} disabled due to {MaxFailures} consecutive failures.", delivery.WebhookId, MaxConsecutiveFailures);
        }

        if (delivery.AttemptCount >= MaxAttempts)
        {
            delivery.Status = WebhookDeliveryStatus.Failed;
            delivery.CompletedAt = _timeProvider.GetUtcNow().UtcDateTime;
        }
        else
        {
            var minutes = delivery.AttemptCount switch
            {
                1 => BackoffMinutesAttempt1,
                2 => BackoffMinutesAttempt2,
                3 => BackoffMinutesAttempt3,
                _ => BackoffMinutesDefault
            };
            
            delivery.NextAttemptAt = _timeProvider.GetUtcNow().UtcDateTime.AddMinutes(minutes);
        }
    }
}
