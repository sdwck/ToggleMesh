using ToggleMesh.CLI.Models;

namespace ToggleMesh.CLI.Core;

public static class NameConverter
{
    public static Dictionary<string, string> GetSafeNamesWithCollisionCheck(IEnumerable<FlagDto> flags, Func<string, string> nameTransformer)
    {
        var safeKeysMap = new Dictionary<string, string>();

        foreach (var flag in flags)
        {
            if (string.IsNullOrWhiteSpace(flag.Key)) 
                continue;
            
            var safeName = nameTransformer(flag.Key);
            
            if (string.IsNullOrEmpty(safeName)) 
                continue;
            
            if (safeKeysMap.TryGetValue(safeName, out var existingOriginalKey))
            {
                throw new InvalidOperationException(
                    $"Collision detected! Both '{existingOriginalKey}' and '{flag.Key}' " +
                    $"resolve to the same property name '{safeName}'. Please rename one in the ToggleMesh Dashboard.");
            }

            safeKeysMap.Add(safeName, flag.Key);
        }

        return safeKeysMap;
    }
    
    public static string ToUpperSnakeCase(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) 
            return string.Empty;

        var words = input.Split(['-', '_'], StringSplitOptions.RemoveEmptyEntries);
        var result = string.Join("_", words.Select(w => w.ToUpperInvariant()));

        if (result.Length > 0 && char.IsDigit(result[0]))
            result = "F" + result;

        return result;
    }

    public static string ToPascalCase(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) 
            return input;
        
        var words = input.Split(['-', '_'], StringSplitOptions.RemoveEmptyEntries);
        
        var result = string.Join("", words.Select(word => 
        {
            if (word.Length == 0) return string.Empty;
            
            var isAllUpper = word.All(c => !char.IsLetter(c) || char.IsUpper(c));
            var restOfWord = isAllUpper ? word[1..].ToLowerInvariant() : word[1..];
            
            return char.ToUpperInvariant(word[0]) + restOfWord;
        }));
        
        if (result.Length > 0 && char.IsDigit(result[0]))
            result = "F" + result;

        return result;
    }
}
