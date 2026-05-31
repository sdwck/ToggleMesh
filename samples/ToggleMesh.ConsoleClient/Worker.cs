using Microsoft.Extensions.Hosting;
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
            Console.WriteLine($"[{DateTime.Now.TimeOfDay}] {feature}: {_toggleMeshClient.IsEnabled(feature, userContext)}");
            await Task.Delay(1000, stoppingToken);
        }
    }
}