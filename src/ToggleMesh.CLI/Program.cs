using Spectre.Console.Cli;
using ToggleMesh.CLI.Commands;

var app = new CommandApp<SyncCommand>();

app.Configure(config =>
{
    config.SetApplicationName("togglemesh");
    config.SetApplicationVersion("1.0.0");
    
    config.AddCommand<SyncCommand>("sync")
        .WithDescription("Synchronizes feature flags from the server.")
        .WithAlias("s");
});

return await app.RunAsync(args);