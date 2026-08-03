using System.Reflection;
using Spectre.Console.Cli;
using ToggleMesh.CLI.Commands;

var version = Assembly.GetExecutingAssembly()
    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
    .InformationalVersion.Split('+')[0] ?? "1.0.0";

var app = new CommandApp();

app.Configure(config =>
{
    config.SetApplicationName("togglemesh");
    config.SetApplicationVersion(version);
    
    config.AddCommand<SyncCommand>("sync")
        .WithDescription("Synchronizes feature flags from the server.")
        .WithAlias("s");
    
    config.AddCommand<ConfigCommand>("config")
        .WithDescription("Configures local and global ToggleMesh settings.")
        .WithAlias("c");
});

return await app.RunAsync(args);
