using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ToggleMesh.ConsoleClient;
using ToggleMesh.SDK.Extensions;

var builder = Host.CreateDefaultBuilder(args);
builder.ConfigureServices((_, services) =>
{
    services.AddToggleMeshClient(options =>
    {
        options.EndpointUrl = "http://localhost:5264";
        // options.ApiKey = "7WgP7EW50dQVpT0C6HT15ig0bXnBGkDQ";
        options.ApiKey = "R0BuW6e1WNRciqmdHsEqEtCROlaEd1nL";
    });
    services.AddHostedService<Worker>();
});

var app = builder.Build();
app.Run();