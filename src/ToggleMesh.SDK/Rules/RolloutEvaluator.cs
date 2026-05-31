namespace ToggleMesh.SDK.Rules;

public static class RolloutEvaluator
{
    public static bool Evaluate(int? rolloutPercentage, string flagKey, string identity)
    {
        if (!rolloutPercentage.HasValue) return true;
        if (rolloutPercentage.Value <= 0) return false;
        if (rolloutPercentage.Value >= 100) return true;
        if (string.IsNullOrWhiteSpace(identity)) return false;

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
        foreach (var c in text)
        {
            hash ^= c;
            hash *= prime;
        }
        return hash;
    }
}