using System.Text;

namespace ToggleMesh.Common.Rules;

public static class RolloutEvaluator
{
    public static bool Evaluate(int? rolloutPercentage, string flagKey, string identity)
    {
        if (!rolloutPercentage.HasValue) 
            return true;
        if (rolloutPercentage.Value <= 0) 
            return false;
        if (rolloutPercentage.Value >= 100) 
            return true;
        if (string.IsNullOrWhiteSpace(identity)) 
            return false;

        var hash = CalculateFnv1aHash(flagKey + identity);
        var bucket = hash % 100;
        
        return bucket < rolloutPercentage.Value;
    }

    // ReSharper disable once InconsistentNaming
    private static uint CalculateFnv1aHash(string text)
    {
        const uint offsetBasis = 2166136261;
        const uint prime = 16777619;
        var hash = offsetBasis;
        
        var byteCount = Encoding.UTF8.GetByteCount(text);
        Span<byte> bytes = byteCount <= 256 ? stackalloc byte[256] : new byte[byteCount];
        
        var written = Encoding.UTF8.GetBytes(text, bytes);

        for (var i = 0; i < written; i++)
        {
            hash ^= bytes[i];
            hash *= prime;
        }
        return hash;
    }
}