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

        return BoxCollisionGeometry2D.TryCollide(
            BoxGeometry2D.From(first),
            BoxGeometry2D.From(second),
            out result);
    }
}
