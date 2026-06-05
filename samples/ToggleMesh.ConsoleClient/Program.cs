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
        options.ApiKey = "tm_mSg9fHyJad78Qw2eWqDWk13eAHFnToXaB0EiU2X2I";
    });
    services.AddHostedService<Worker>();
});

var app = builder.Build();
app.Run();