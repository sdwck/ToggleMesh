namespace ToggleMesh.Common;

public readonly struct VariationWeight : IEquatable<VariationWeight>
{
    public Guid VariationId { get; init; }
    public int Weight { get; init; }

    public VariationWeight(Guid variationId, int weight)
    {
        VariationId = variationId;
        Weight = weight;
    }

    public bool Equals(VariationWeight other) =>
        VariationId.Equals(other.VariationId) && Weight == other.Weight;

    public override bool Equals(object? obj) =>
        obj is VariationWeight other && Equals(other);

    public override int GetHashCode() =>
        HashCode.Combine(VariationId, Weight);

    public static bool operator ==(VariationWeight left, VariationWeight right) =>
        left.Equals(right);

    public static bool operator !=(VariationWeight left, VariationWeight right) => 
        !left.Equals(right);
}
