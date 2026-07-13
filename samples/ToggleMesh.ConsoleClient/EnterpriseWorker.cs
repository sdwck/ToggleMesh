using Microsoft.Extensions.Hosting;
using ToggleMesh.SDK.Clients;

namespace ToggleMesh.ConsoleClient;

public class EnterpriseWorker : BackgroundService
{
    private readonly IToggleMeshClient _toggleMeshClient;

    public EnterpriseWorker(IToggleMeshClient toggleMeshClient)
    {
        _toggleMeshClient = toggleMeshClient;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(1000, stoppingToken);
        
        Console.WriteLine("Starting Enterprise Simulation Worker...");

        var flags = new[]
        {
            "new_checkout_flow",
            "dark_mode_theme",
            "ai_recommendations",
            "one_click_buy",
            "loyalty_program_banner",
            "fast_shipping_promo",
            "crypto_payment_method",
            "chat_bot_support",
            "video_product_reviews",
            "gamification_dashboard"
        };

        var tracks = new[]
        {
            "checkout_completed",
            "item_added",
            "theme_changed",
            "recommendation_clicked",
            "buy_clicked",
            "promo_applied",
            "crypto_selected",
            "chat_opened",
            "video_played",
            "achievement_unlocked"
        };

        var countries = new[] { "US", "UK", "CA", "DE", "FR", "JP", "AU" };
        
        var countryProfiles = new Dictionary<string, (double controlRate, double treatmentRate)>
        {
            ["US"] = (0.13, 0.16),
            ["UK"] = (0.14, 0.145),
            ["CA"] = (0.12, 0.125),
            ["DE"] = (0.11, 0.14),
            ["FR"] = (0.15, 0.12),
            ["JP"] = (0.10, 0.105),
            ["AU"] = (0.14, 0.11)
        };

        var lastLogTime = DateTime.UtcNow;
        var trackedCount = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            for (var i = 0; i < 50; i++)
            {
                var userId = $"enterprise-user-{Guid.NewGuid().ToString("N").Substring(0, 8)}";
                var country = countries[Random.Shared.Next(countries.Length)];
                var userContext = new AotUserContext
                {
                    UserId = Random.Shared.Next(1, 100000),
                    Email = $"{userId}@example.com",
                    Country = country
                };
                
                var flagsToEvaluate = flags.OrderBy(_ => 
                    Random.Shared.Next())
                    .Take(Random.Shared.Next(3, 8))
                    .ToList();

                foreach (var flag in flagsToEvaluate)
                {
                    var isEnabled = _toggleMeshClient.IsEnabled(
                        flag, 
                        userId, 
                        ref userContext);

                    var profile = countryProfiles[country];
                    var chanceToTrack = isEnabled 
                        ? profile.treatmentRate 
                        : profile.controlRate;
                    
                    chanceToTrack += (Random.Shared.NextDouble() - 0.5) * 0.06;
                    chanceToTrack = Math.Clamp(chanceToTrack, 0.01, 0.99);

                    if (Random.Shared.NextDouble() < chanceToTrack)
                    {
                        var trackToTrigger = tracks[Random.Shared.Next(tracks.Length)];
                        var baseValue = 5.0 + Random.Shared.NextDouble() * 45.0;
                        var countryMultiplier = country switch
                        {
                            "US" => 1.2,
                            "DE" => 1.1,
                            "UK" => 1.15,
                            "JP" => 0.9,
                            _ => 1.0
                        };
                        _toggleMeshClient.Track(
                            trackToTrigger, 
                            userId, userContext, 
                            value: baseValue * countryMultiplier);
                        
                        trackedCount++;
                    }
                }
            }
            
            if ((DateTime.UtcNow - lastLogTime).TotalSeconds >= 5)
            {
                Console.WriteLine($"[EnterpriseSimulationWorker] Emitted {trackedCount} track events in the last 5 seconds.");
                trackedCount = 0;
                lastLogTime = DateTime.UtcNow;
            }

            await Task.Delay(200, stoppingToken);
        }
    }
}
