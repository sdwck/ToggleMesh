using System.Text.RegularExpressions;

namespace ToggleMesh.CLI.Core.Parsers;

public partial class GoFileParser : FileParser
{
    [GeneratedRegex(@"^\s*(?<name>[A-Z]\w*)\s*=\s*['""]", RegexOptions.Multiline)]
    private static partial Regex MyRegex();

    protected override Regex GetRegex() => MyRegex();
}
