using Microsoft.Extensions.Configuration;

namespace ToggleMesh.CLI.Core;

public static class ConfigResolver
{
    public static (string? ApiKey, string? Address) ResolveFallbackConfig()
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();
        
        var apiKey = config["ToggleMesh:ApiKey"];
        var address = config["ToggleMesh:Address"];
        
        return (apiKey, address);
    }
}