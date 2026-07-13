using System.Text.RegularExpressions;

namespace ToggleMesh.CLI.Core.Parsers;

public partial class JsAndTsFileParser : FileParser
{
    [GeneratedRegex(@"^\s*(?<name>\w+)\s*:\s*['""]", RegexOptions.Multiline)]
    private static partial Regex MyRegex();
    protected override Regex GetRegex() => MyRegex();
}
