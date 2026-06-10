namespace ToggleMesh.CLI.Core;

public static class LangDetector
{
    public static string DiscoverLanguage()
    {
        var cwd = Directory.GetCurrentDirectory();

        var isNpm = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("npm_config_user_agent")) ||
                    !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("npm_lifecycle_event"));

        if (isNpm || File.Exists(Path.Combine(cwd, "package.json")))
            return File.Exists(Path.Combine(cwd, "tsconfig.json")) 
                ? "typescript" 
                : "javascript";

        var isDotNet = Directory.GetFiles(cwd, "*.csproj").Length != 0 ||
                       Directory.GetFiles(cwd, "*.sln").Length != 0;

        // ReSharper disable once ConvertIfStatementToReturnStatement
        if (isDotNet)
            // ReSharper disable once DuplicatedStatements
            return "csharp";

        return "csharp";
    }
}