using Silk.NET.Maths;

namespace Schreadt_Engine.Collision;

public sealed class CircleCircleNarrowPhase2D
    : ICollisionNarrowPhase2D<CircleCollider2D, CircleCollider2D>
{
    public bool TryCollide(
        CircleCollider2D first,
        CircleCollider2D second,
        out CollisionResult2D result)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        var offset = second.Center - first.Center;
        var distanceSquared = offset.X * offset.X + offset.Y * offset.Y;
        var combinedRadius = first.Radius + second.Radius;

        if (distanceSquared > combinedRadius * combinedRadius)
        {
            result = default;
            return false;
        }

        var distance = Math.Sqrt(distanceSquared);
        var normal = distance > double.Epsilon
            ? offset / distance
            : new Vector2D<double>(1.0, 0.0);

        var penetration = combinedRadius - distance;
        var firstSurface = first.Center + normal * first.Radius;
        var secondSurface = second.Center - normal * second.Radius;
        result = new CollisionResult2D(normal, penetration, (firstSurface + secondSurface) * 0.5);
        return true;
    }
}
