namespace Schreadt_Engine.Collision;

/// <summary>
/// A compact set of the collision layers a collider accepts.
/// </summary>
public readonly record struct CollisionLayerMask2D(uint Bits)
{
    public const int LayerCount = 32;

    public static CollisionLayerMask2D None { get; } = new(0u);
    public static CollisionLayerMask2D All { get; } = new(uint.MaxValue);

    public static CollisionLayerMask2D FromLayers(params int[] layers)
    {
        ArgumentNullException.ThrowIfNull(layers);

        var bits = 0u;
        foreach (var layer in layers) bits |= GetLayerBit(layer);
        return new CollisionLayerMask2D(bits);
    }

    public bool Contains(int layer) => (Bits & GetLayerBit(layer)) != 0u;

    public CollisionLayerMask2D WithLayer(int layer) => new(Bits | GetLayerBit(layer));

    public CollisionLayerMask2D WithoutLayer(int layer) => new(Bits & ~GetLayerBit(layer));

    public static CollisionLayerMask2D operator |(CollisionLayerMask2D first, CollisionLayerMask2D second)
        => new(first.Bits | second.Bits);

    public static CollisionLayerMask2D operator &(CollisionLayerMask2D first, CollisionLayerMask2D second)
        => new(first.Bits & second.Bits);

    public static CollisionLayerMask2D operator ~(CollisionLayerMask2D mask) => new(~mask.Bits);

    internal static void ValidateLayer(int layer, string parameterName)
    {
        if (layer is < 0 or >= LayerCount)
            throw new ArgumentOutOfRangeException(parameterName, $"Collision layers must be between 0 and {LayerCount - 1}.");
    }

    private static uint GetLayerBit(int layer)
    {
        ValidateLayer(layer, nameof(layer));
        return 1u << layer;
    }
}
