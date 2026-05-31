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

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var feature = "test-feature";
        while (!stoppingToken.IsCancellationRequested)
        {
            Console.WriteLine($"[{DateTime.Now.TimeOfDay}] {feature}: {_toggleMeshClient.IsEnabled(feature)}");
            await Task.Delay(1000, stoppingToken);
        }
    }
}