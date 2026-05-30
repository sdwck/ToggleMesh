using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ToggleMesh.ConsoleClient;
using ToggleMesh.SDK.Extensions;

var builder = Host.CreateDefaultBuilder(args);
builder.ConfigureServices((_, services) =>
{
    services.AddToggleMeshClient(options =>
    {
        options.EndpointUrl = "https://localhost:7282";
    });
    services.AddHostedService<Worker>();
});

var app = builder.Build();
app.Run();