using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ToggleMesh.ConsoleClient;
using ToggleMesh.SDK.Extensions;

var builder = Host.CreateDefaultBuilder(args);

builder.ConfigureServices((context, services) =>
{
    services.AddToggleMeshClient(options =>
    {
        options.BaseUrl = context.Configuration["ToggleMesh:BaseUrl"]!;
        options.ApiKey = context.Configuration["ToggleMesh:ApiKey"]!;
        options.MaxBatchSize = 5000;
    });
    services.AddHostedService<ShowcaseWorker>();
    // services.AddHostedService<MabSimulationWorker>();
});

var app = builder.Build();
app.Run();