using System.Text.RegularExpressions;

namespace ToggleMesh.CLI.Core.Parsers;

public partial class CSharpFileParser : FileParser
{
    [GeneratedRegex(@"public const string\s+(?<name>\w+)\s*=")]
    private static partial Regex MyRegex();
    protected override Regex GetRegex() => MyRegex();
}