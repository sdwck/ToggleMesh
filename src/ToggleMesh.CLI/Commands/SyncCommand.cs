using Spectre.Console;
using Spectre.Console.Cli;
using ToggleMesh.CLI.Core;
using ToggleMesh.CLI.Core.Parsers;
using ToggleMesh.CLI.Generators;
using ToggleMesh.CLI.Models;

// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable ClassNeverInstantiated.Global

namespace ToggleMesh.CLI.Commands;

public class SyncSettings : CommandSettings
{
    [CommandOption("-k|--api-key")] public string? ApiKey { get; set; }

    [CommandOption("-a|--address")] public string? Address { get; set; }

    [CommandOption("-l|--lang")] public string? Lang { get; set; }

    [CommandOption("-o|--out")] public string? OutPath { get; set; }
    [CommandOption("-p|--project-id")] public string? ProjectId { get; set; }
    [CommandOption("-y|--yes")] public bool AutoApply { get; set; }
}

public class SyncCommand : AsyncCommand<SyncSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, SyncSettings settings,
        CancellationToken cancellationToken)
    {
        AnsiConsole.MarkupLine("[bold cyan]ToggleMesh CLI[/] - Feature Flag Synchronization\n");

        var (configApiKey, configAddress, configProjectId, configLang, configOutPath) = ConfigResolver.ResolveConfig();
        var apiKey = settings.ApiKey ?? configApiKey;
        var address = settings.Address ?? configAddress;
        var projectId = settings.ProjectId ?? configProjectId;
        var lang = settings.Lang ?? configLang ?? LangDetector.DiscoverLanguage();

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            AnsiConsole.MarkupLine(
                "[red]Error:[/] API Key is missing. Provide it via --api-key or in appsettings.json.");
            return -1;
        }

        if (string.IsNullOrWhiteSpace(address))
        {
            AnsiConsole.MarkupLine(
                "[red]Error:[/] API Address is missing. Provide it via --address or in appsettings.json.");
            return -1;
        }

        var isPat = apiKey.StartsWith("tmp_", StringComparison.OrdinalIgnoreCase);

        if (isPat && string.IsNullOrWhiteSpace(projectId))
        {
            AnsiConsole.MarkupLine(
                "[red]Error:[/] Personal Access Token (PAT) detected, but [bold]--project-id[/] is missing. Please provide your Project ID.");
            return -1;
        }

        AnsiConsole.MarkupLine($"[grey]Target Language:[/] [bold]{lang}[/]");
        AnsiConsole.MarkupLine($"[grey]ToggleMesh Address:[/] [bold]{address}[/]\n");

        using var httpClient = new HttpClient();
        httpClient.BaseAddress = new Uri(address);

        if (isPat)
        {
            httpClient.DefaultRequestHeaders.Add("x-pat-token", apiKey);
            httpClient.DefaultRequestHeaders.Add("x-environment-id", projectId);
        }
        else
            httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);

        var apiClient = new ApiClient(httpClient);
        var flags = new List<FlagDto>();

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Fetching flags from Control Plane...",
                async _ =>
                {
                    flags = await apiClient.GetAllFlagsAsync(isPat ? projectId : null, cancellationToken);
                });

        if (flags.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]Warning:[/] No feature flags found for this environment.");
            if (!settings.AutoApply)
            {
                var shouldContinue = await AnsiConsole.ConfirmAsync(
                    "Do you want to continue anyway?",
                    defaultValue: false, cancellationToken);

                if (!shouldContinue)
                {
                    AnsiConsole.MarkupLine("[grey]Operation aborted by user.[/]");
                    return 0;
                }
            }
        }

        try
        {
            Func<string, string> transformer = lang.ToLower() switch
            {
                "python" or "py" => NameConverter.ToUpperSnakeCase,
                _ => NameConverter.ToPascalCase
            };

            var mappedKeys = NameConverter.GetSafeNamesWithCollisionCheck(flags, transformer);

            ICodeGenerator generator = lang.ToLower() switch
            {
                "typescript" or "ts" => new TypeScriptGenerator(),
                "csharp" or "cs" => new CSharpGenerator(),
                "javascript" or "js" => new JavaScriptGenerator(),
                "python" or "py" => new PythonGenerator(),
                "go" or "golang" => new GoGenerator(),
                _ => throw new NotSupportedException(
                    $"Language '{lang}' is not supported yet. Select from ['typescript', 'csharp'].")
            };

            FileParser parser = lang.ToLower() switch
            {
                "typescript" or "ts" or "javascript" or "js" => new JsAndTsFileParser(),
                "csharp" or "cs" => new CSharpFileParser(),
                "python" or "py" => new PythonFileParser(),
                "go" or "golang" => new GoFileParser(),
                _ => throw new NotSupportedException()
            };

            var path = settings.OutPath ?? configOutPath ?? Directory.GetCurrentDirectory();
            if (Directory.Exists(path))
                path = Path.Combine(path, generator.DefaultFileName);

            var newProperties = mappedKeys.Keys.ToHashSet();
            var oldProperties = parser.GetExistingProperties(path);

            var added = newProperties.Except(oldProperties).OrderBy(x => x).ToList();
            var removed = oldProperties.Except(newProperties).OrderBy(x => x).ToList();
            var unchanged = newProperties.Intersect(oldProperties).OrderBy(x => x).ToList();

            var table = new Table()
                .Border(TableBorder.Rounded)
                .Title("[bold]Proposed Changes[/]");
            table.AddColumn("St.");
            table.AddColumn("Property");

            foreach (var k in added)
                table.AddRow("[bold green][[+]][/]", $"[bold green]{k}[/]");

            foreach (var k in removed)
                table.AddRow("[bold red][[-]][/]", $"[strikethrough red]{k}[/]");

            if (added.Count == 0 && removed.Count == 0 && unchanged.Count > 0 && File.Exists(path))
            {
                AnsiConsole.MarkupLine($"[green]Everything is up to date![/] ({unchanged.Count} flags synced).");
                return 0;
            }

            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();

            if (removed.Count > 0)
            {
                AnsiConsole.MarkupLine(
                    "[bold red]WARNING:[/] This sync will remove some flags! Your project might not compile if they are currently in use.");

                if (!settings.AutoApply && !await AnsiConsole.ConfirmAsync("Do you want to apply these changes?",
                        defaultValue: true, cancellationToken: cancellationToken))
                {
                    AnsiConsole.MarkupLine("[grey]Operation aborted by user.[/]");
                    return 0;
                }
            }

            var code = generator.Generate(mappedKeys);
            await File.WriteAllTextAsync(path, code, cancellationToken);

            AnsiConsole.MarkupLine(
                $"[green]✔  Success![/] Generated {mappedKeys.Count} flags to [bold]{Path.GetFileName(path)}[/]");
            return 0;
        }
        catch (Exception ex)
        {
            var safeMessage = ex.Message.EscapeMarkup();
            AnsiConsole.MarkupLine($"\n[red]Fatal Error:[/] {safeMessage}");
            return -1;
        }
    }
}
