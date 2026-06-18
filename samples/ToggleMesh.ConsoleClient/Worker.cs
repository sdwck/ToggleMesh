using Microsoft.Extensions.Hosting;
using ToggleMesh.Generated;
using ToggleMesh.SDK.Clients;

namespace ToggleMesh.ConsoleClient;

public class Worker : BackgroundService
{
    private readonly IToggleMeshClient _toggleMeshClient;

    public Worker(IToggleMeshClient toggleMeshClient)
    {
        _toggleMeshClient = toggleMeshClient;
    }

    private record UserContext(int UserId, string Email, string Country);
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var feature = "gmail-20percent";
        var userContext = new UserContext(3, "nirawolker@gmail.com", "US");
        
        while (!stoppingToken.IsCancellationRequested)
        {
            Console.WriteLine($"[{DateTime.Now.TimeOfDay}] {feature}: {_toggleMeshClient.IsEnabled(Flags.Gmail20percent, userContext)}");
            Console.WriteLine($"[{DateTime.Now.TimeOfDay}] prod-feat: {_toggleMeshClient.IsEnabled("prod-feat", userContext)}");
            Console.WriteLine($"[{DateTime.Now.TimeOfDay}] prod-feat2: {_toggleMeshClient.IsEnabled("prod-feat2", userContext)}");
            await Task.Delay(3000, stoppingToken);
        }
    }
}