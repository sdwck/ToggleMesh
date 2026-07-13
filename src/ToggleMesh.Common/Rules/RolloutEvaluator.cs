// ReSharper disable ForCanBeConvertedToForeach
namespace ToggleMesh.Common.Rules;

public static class RolloutEvaluator
{
    public static Guid? Evaluate(VariationWeight[]? rollout, Guid? offVariationId, string flagKey, string identity)
    {
        if (rollout == null || rollout.Length == 0) 
            return offVariationId;

        if (rollout.Length == 1 && rollout[0].Weight >= 10000)
            return rollout[0].VariationId;

        if (string.IsNullOrWhiteSpace(identity)) 
            return offVariationId;

        var hash = CalculateFnv1aHash(flagKey, identity);
        var bucket = hash % 10000;
        
        long cumulative = 0;
        for (var i = 0; i < rollout.Length; i++)
        {
            cumulative += rollout[i].Weight;
            if (bucket < cumulative)
                return rollout[i].VariationId;
        }

        return offVariationId;
    }

    // ReSharper disable once InconsistentNaming
    private static uint CalculateFnv1aHash(string key1, string key2)
    {
        const uint offsetBasis = 2166136261;
        const uint prime = 16777619;
        var hash = offsetBasis;
        
        for (var i = 0; i < key1.Length; i++)
        {
            hash ^= key1[i];
            hash *= prime;
        }

        for (var i = 0; i < key2.Length; i++)
        {
            hash ^= key2[i];
            hash *= prime;
        }
        
        return hash;
    }
}
