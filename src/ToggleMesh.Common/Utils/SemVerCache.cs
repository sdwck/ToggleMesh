using System.Collections.Concurrent;
using NuGet.Versioning;

namespace ToggleMesh.Common.Utils;

public static class SemVerCache
{
    private static readonly ConcurrentDictionary<string, NuGetVersion> Cache = new(StringComparer.Ordinal);
    private static readonly char[] TrimChars = ['v', 'V'];

    public static bool TryParse(string? version, out NuGetVersion result)
    {
        if (version == null)
        {
            result = null!;
            return false;
        }

        if (Cache.TryGetValue(version, out var cached))
        {
            result = cached;
            return true;
        }

        if (NuGetVersion.TryParse(version.TrimStart(TrimChars), out var parsed))
        {
            if (Cache.Count < 1000)
            {
                Cache.TryAdd(version, parsed);
            }
            result = parsed;
            return true;
        }

        result = null!;
        return false;
    }
}
