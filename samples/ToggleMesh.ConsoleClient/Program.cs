using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ToggleMesh.ConsoleClient;
using ToggleMesh.SDK.Extensions;

var builder = Host.CreateDefaultBuilder(args);
builder.ConfigureServices((context, services) =>
{
    services.AddToggleMeshClient(options =>
    {
        options.EndpointUrl = context.Configuration["ToggleMesh:Address"] ?? throw new Exception("ToggleMesh__Address is missing");
        options.ApiKey = context.Configuration["ToggleMesh:ApiKey"] ?? throw new Exception("ToggleMesh__ApiKey is missing");
    });
    services.AddHostedService<Worker>();
});

var app = builder.Build();
app.Run();