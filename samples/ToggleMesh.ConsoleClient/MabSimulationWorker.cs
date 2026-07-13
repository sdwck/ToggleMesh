using Microsoft.Extensions.Hosting;

namespace ToggleMesh.ConsoleClient;

public class MabSimulationWorker : BackgroundService
{
    private readonly SDK.Clients.IToggleMeshClient _client;

    public MabSimulationWorker(SDK.Clients.IToggleMeshClient client) => _client = client;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Console.WriteLine("C# SDK MAB Simulation started (no_browser -> Second)");

        while (!stoppingToken.IsCancellationRequested)
        {
            for (var i = 0; i < 10; i++)
            {
                var userId = Guid.NewGuid().ToString();
                
                var variation = _client.GetStringVariation("mab-string-test", userId, new { Browser = "no_browser" }, "default-variant");
                
                Console.WriteLine($"[C# SDK] Evaluated mab-string-test for {userId}: {variation}");
                
                var prob = variation == "Second" ? 0.155 : 0.145;
                if (Random.Shared.NextDouble() < prob)
                    _client.Track("purchase", userId, new { sdk = "csharp" }, 10.0);
            }
            await Task.Delay(1000, stoppingToken);
        }
    }
}
