using System.Text.RegularExpressions;

namespace ToggleMesh.CLI.Core.Parsers;

public abstract partial class FileParser
{
    protected abstract Regex GetRegex();

    public HashSet<string> GetExistingProperties(string filePath)
    {
        if (!File.Exists(filePath))
            return [];

        var content = File.ReadAllText(filePath);
        var keys = new HashSet<string>();

        foreach (Match match in GetRegex().Matches(content))
        {
            keys.Add(match.Groups["name"].Value);
        }

        return keys;
    }
}