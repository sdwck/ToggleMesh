using System.Text.RegularExpressions;

namespace ToggleMesh.CLI.Core.Parsers;

public partial class PythonFileParser : FileParser
{
    [GeneratedRegex(@"^\s*(?<name>[A-Z0-9_]+)\s*=\s*['""]", RegexOptions.Multiline)]
    private static partial Regex MyRegex();

    protected override Regex GetRegex() => MyRegex();
}