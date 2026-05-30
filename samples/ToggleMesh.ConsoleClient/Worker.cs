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
        while (!stoppingToken.IsCancellationRequested)
        {
            Console.WriteLine("test-feature: " + _toggleMeshClient.IsEnabled("test-feature"));
            await Task.Delay(1000, stoppingToken);
        }
    }
}