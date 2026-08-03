using Silk.NET.Maths;

namespace Schreadt_Engine.Collision;

public sealed class BoxBoxNarrowPhase2D
    : ICollisionNarrowPhase2D<AxisAlignedBoxCollider2D, AxisAlignedBoxCollider2D>
{
    public bool TryCollide(
        AxisAlignedBoxCollider2D first,
        AxisAlignedBoxCollider2D second,
        out CollisionResult2D result)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        var centerOffset = second.Center - first.Center;
        var overlapX = first.HalfSize.X + second.HalfSize.X - Math.Abs(centerOffset.X);
        if (overlapX < 0.0)
        {
            result = default;
            return false;
        }

        var overlapY = first.HalfSize.Y + second.HalfSize.Y - Math.Abs(centerOffset.Y);
        if (overlapY < 0.0)
        {
            result = default;
            return false;
        }

        if (overlapX <= overlapY)
        {
            var direction = centerOffset.X < 0.0 ? -1.0 : 1.0;
            result = new CollisionResult2D(new Vector2D<double>(direction, 0.0), overlapX);
        }
        else
        {
            var direction = centerOffset.Y < 0.0 ? -1.0 : 1.0;
            result = new CollisionResult2D(new Vector2D<double>(0.0, direction), overlapY);
        }

        return true;
    }
}
