using Silk.NET.Maths;

namespace Schreadt_Engine.Collision;

public readonly record struct CollisionQueryFilter2D
{
    public CollisionLayerMask2D LayerMask { get; }

    public bool IncludeTriggers { get; }

    public Predicate<Collider2D>? Predicate { get; }

    public CollisionQueryFilter2D(
        CollisionLayerMask2D layerMask,
        bool includeTriggers = true,
        Predicate<Collider2D>? predicate = null)
    {
        LayerMask = layerMask;
        IncludeTriggers = includeTriggers;
        Predicate = predicate;
    }

    public static CollisionQueryFilter2D All { get; } = new(CollisionLayerMask2D.All);

    internal bool Allows(Collider2D collider)
    {
        return LayerMask.Contains(collider.CollisionLayer) &&
               (IncludeTriggers || !collider.IsTrigger) &&
               (Predicate?.Invoke(collider) ?? true);
    }
}

public readonly record struct RaycastHit2D
{
    public Collider2D Collider { get; }

    public Vector2D<double> Point { get; }

    public Vector2D<double> Normal { get; }

    public double Distance { get; }

    public double Fraction { get; }

    internal RaycastHit2D(
        Collider2D collider,
        Vector2D<double> point,
        Vector2D<double> normal,
        double distance,
        double maximumDistance)
    {
        Collider = collider;
        Point = point;
        Normal = normal;
        Distance = distance;
        Fraction = maximumDistance > 0.0 ? distance / maximumDistance : 0.0;
    }
}
