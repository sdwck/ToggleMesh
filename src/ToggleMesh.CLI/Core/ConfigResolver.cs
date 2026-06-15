using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Spectre.Console;
using ToggleMesh.CLI.Models;

namespace ToggleMesh.CLI.Core;

public static class ConfigResolver
{
    private static readonly string GlobalConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".togglemesh");

    private static readonly string GlobalConfigPath = Path.Combine(GlobalConfigDir, "config.json");

    private static readonly string LocalConfigDir = Path.Combine(Directory.GetCurrentDirectory(), ".togglemesh");
    private static readonly string LocalConfigPath = Path.Combine(LocalConfigDir, "config.json");

    public static (string? ApiKey, string? BaseUrl, string? ProjectId, string? Lang, string? OutPath) ResolveConfig()
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true);

        if (File.Exists(GlobalConfigPath))
            builder.AddJsonFile(GlobalConfigPath, optional: true);

        if (File.Exists(LocalConfigPath))
            builder.AddJsonFile(LocalConfigPath, optional: true);

        var config = builder
            .AddEnvironmentVariables(prefix: "TOGGLEMESH__")
            .Build();

        var apiKey = config["ToggleMesh:ApiKey"] ?? config["ApiKey"];
        var address = config["ToggleMesh:BaseUrl"] ?? config["BaseUrl"];
        var projectId = config["ToggleMesh:ProjectId"] ?? config["ProjectId"];
        var lang = config["ToggleMesh:Lang"] ?? config["Lang"];
        var outPath = config["ToggleMesh:OutPath"] ?? config["OutPath"];

        return (apiKey, address, projectId, lang, outPath);
    }

    public static bool SaveConfig(string? apiKey, string? address, string? projectId, string? lang, string? outPath)
    {
        try
        {
            if (!Directory.Exists(GlobalConfigDir))
                Directory.CreateDirectory(GlobalConfigDir);
            

            if (!string.IsNullOrWhiteSpace(apiKey) || !string.IsNullOrWhiteSpace(address))
            {
                var globalData = new Dictionary<string, string>();
                if (File.Exists(GlobalConfigPath))
                {
                    try
                    {
                        var content = File.ReadAllText(GlobalConfigPath);
                        if (!string.IsNullOrWhiteSpace(content))
                        {
                            var existing = JsonSerializer.Deserialize(content, ToggleMeshJsonContext.Default.DictionaryStringString);
                            if (existing != null) 
                                globalData = existing;
                        }
                    }
                    catch
                    {
                        // ignore
                    }
                }

                if (!string.IsNullOrWhiteSpace(apiKey)) 
                    globalData["ApiKey"] = apiKey;
                if (!string.IsNullOrWhiteSpace(address)) 
                    globalData["BaseUrl"] = address;

                var globalJson = JsonSerializer.Serialize(globalData, ToggleMeshJsonContext.Default.DictionaryStringString);
                File.WriteAllText(GlobalConfigPath, globalJson);
            }

            if (string.IsNullOrWhiteSpace(projectId)
                && string.IsNullOrWhiteSpace(lang)
                && string.IsNullOrWhiteSpace(outPath))
                return false;

            var localDir = LocalConfigDir;
            if (!Directory.Exists(localDir))
                Directory.CreateDirectory(localDir);

            var localData = new Dictionary<string, string>();
            if (File.Exists(LocalConfigPath))
            {
                try
                {
                    var content = File.ReadAllText(LocalConfigPath);
                    if (!string.IsNullOrWhiteSpace(content))
                    {
                        var existing = JsonSerializer.Deserialize(content, ToggleMeshJsonContext.Default.DictionaryStringString);
                        if (existing != null) 
                            localData = existing;
                    }
                }
                catch
                {
                    // ignore
                }
            }

            if (!string.IsNullOrWhiteSpace(projectId))
                localData["ProjectId"] = projectId;
            if (!string.IsNullOrWhiteSpace(lang))
                localData["Lang"] = lang;
            if (!string.IsNullOrWhiteSpace(outPath))
                localData["OutPath"] = outPath;

            var localJson = JsonSerializer.Serialize(localData, ToggleMeshJsonContext.Default.DictionaryStringString);
            File.WriteAllText(LocalConfigPath, localJson);

            return true;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Configuration Save Error:[/] {ex.Message}");
        }
        
        return false;
    }
}