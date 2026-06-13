using Spectre.Console;
using Spectre.Console.Cli;
using ToggleMesh.CLI.Core;

namespace ToggleMesh.CLI.Commands;

public class ConfigSettings : CommandSettings
{
    [CommandOption("-k|--api-key")]
    public string? ApiKey { get; set; }

    [CommandOption("-a|--address")]
    public string? Address { get; set; }

    [CommandOption("-p|--project-id")]
    public string? ProjectId { get; set; }

    [CommandOption("-l|--lang")]
    public string? Lang { get; set; }

    [CommandOption("-o|--out")]
    public string? OutPath { get; set; }
}

public class ConfigCommand : AsyncCommand<ConfigSettings>
{
    protected override Task<int> ExecuteAsync(CommandContext context, ConfigSettings settings, CancellationToken cancellationToken)
    {
        AnsiConsole.MarkupLine("[bold cyan]ToggleMesh CLI[/] - Configuration Wizard\n");

        var apiKey = settings.ApiKey;
        var address = settings.Address;
        var projectId = settings.ProjectId;
        var lang = settings.Lang;
        var outPath = settings.OutPath;

        if (string.IsNullOrWhiteSpace(apiKey) && 
            string.IsNullOrWhiteSpace(address) && 
            string.IsNullOrWhiteSpace(projectId) && 
            string.IsNullOrWhiteSpace(lang) && 
            string.IsNullOrWhiteSpace(outPath))
        {
            AnsiConsole.MarkupLine("[grey]Interactive configuration mode started. Press Ctrl+C to abort.[/]\n");

            var usePat = AnsiConsole.Confirm("Are you configuring with a Personal Access Token (PAT)?", true);

            if (usePat)
            {
                apiKey = AnsiConsole.Prompt(
                    new TextPrompt<string>("Enter your [bold]Personal Access Token (tmp_...)[/]:")
                        .PromptStyle("green")
                        .Secret());
                
                projectId = AnsiConsole.Prompt(
                    new TextPrompt<string>("Enter your [bold]Project ID (Guid)[/]:")
                        .PromptStyle("green"));
            }
            else
            {
                apiKey = AnsiConsole.Prompt(
                    new TextPrompt<string>("Enter your [bold]Environment API Key (tm_server_...)[/]:")
                        .PromptStyle("green")
                        .Secret());
            }

            address = AnsiConsole.Prompt(
                new TextPrompt<string>("Enter your [bold]ToggleMesh Address[/]:")
                    .DefaultValue("http://localhost:5264")
                    .PromptStyle("green"));

            lang = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Select your target programming language:")
                    .PageSize(10)
                    .AddChoices(["csharp", "typescript", "javascript", "python", "go"]));

            var rawOutPath = AnsiConsole.Prompt(
                new TextPrompt<string>("Enter output file path (leave empty for default):")
                    .AllowEmpty()
                    .PromptStyle("green"));
            
            outPath = string.IsNullOrWhiteSpace(rawOutPath) ? null : rawOutPath.Trim();
        }

        var result = ConfigResolver.SaveConfig(apiKey, address, projectId, lang, outPath);
        if (!result)
        {
            AnsiConsole.MarkupLine("\n[red]✗ Error![/] Failed to save configuration. Please verify your inputs and directory permissions.");
            return Task.FromResult(-1);
        }

        AnsiConsole.MarkupLine("\n[green]✔ Success![/] Configuration saved successfully.");
        AnsiConsole.MarkupLine("[grey]- Global credentials saved to: ~/.togglemesh/config.json[/]");
        if (!string.IsNullOrWhiteSpace(projectId) || !string.IsNullOrWhiteSpace(lang) || !string.IsNullOrWhiteSpace(outPath))
            AnsiConsole.MarkupLine("[grey]- Project settings saved to: ./.togglemesh/config.json[/]");

        return Task.FromResult(0);
    }
}