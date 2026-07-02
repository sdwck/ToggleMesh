using Microsoft.Extensions.Hosting;
using ToggleMesh.SDK.Attributes;
using ToggleMesh.SDK.Clients;

namespace ToggleMesh.ConsoleClient;

[ToggleMeshContext]
public partial struct AotUserContext
{
    public int UserId { get; set; }
    public string Email { get; set; }
    public string Country { get; set; }
}

public class ReflectionUserContext
{
    public int UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
}

public class Worker : BackgroundService
{
    private readonly IToggleMeshClient _toggleMeshClient;

    public Worker(IToggleMeshClient toggleMeshClient)
    {
        _toggleMeshClient = toggleMeshClient;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            Console.WriteLine($"\n[{DateTime.Now.TimeOfDay}] --- Evaluating Feature Flags ---");

            DemonstrateMabTrafficSimulation();
            DemonstrateGmail20PercentFlag();

            await Task.Delay(3000, stoppingToken);
        }
    }

    private void DemonstrateGmail20PercentFlag()
    {
        var email = "nirawolker@gmail.com";
        var context = new AotUserContext { Email = email, UserId = 123456 };
        var isEnabled = _toggleMeshClient.IsEnabled("gmail-20percent", ref context);
        
        if (isEnabled)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"[Gmail 20%] {email} -> ENABLED!");
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine($"[Gmail 20%] {email} -> DISABLED.");
        }
        Console.ResetColor();
    }

    private void DemonstrateMabTrafficSimulation()
    {
        var userId = $"virtual-user-{Guid.NewGuid().ToString("N").Substring(0, 8)}";
        var isEnabled = _toggleMeshClient.IsEnabled("mab-test", identity: userId);
        
        var rand = Random.Shared.NextDouble();
        var conversionProbability = isEnabled ? 0.15 : 0.10;

        if (rand <= conversionProbability)
        {
            var value = 5 + (Random.Shared.NextDouble() * 45) + (isEnabled ? 7.5 : 0);
            _toggleMeshClient.Track("mab_converted", identity: userId, value: value);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[MAB Traffic] User {userId} got Variant {(isEnabled ? "TRUE" : "FALSE")} and CONVERTED! (${value:F2}) 🎯");
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"[MAB Traffic] User {userId} got Variant {(isEnabled ? "TRUE" : "FALSE")} and bounced.");
        }

        Console.ResetColor();
    }
}