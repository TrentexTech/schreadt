namespace Schreadt_Engine.Collision;

public sealed class OrientedBoxOrientedBoxNarrowPhase2D
    : ICollisionNarrowPhase2D<OrientedBoxCollider2D, OrientedBoxCollider2D>
{
    public bool TryCollide(
        OrientedBoxCollider2D first,
        OrientedBoxCollider2D second,
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

public sealed class AxisAlignedBoxOrientedBoxNarrowPhase2D
    : ICollisionNarrowPhase2D<AxisAlignedBoxCollider2D, OrientedBoxCollider2D>
{
    public bool TryCollide(
        AxisAlignedBoxCollider2D first,
        OrientedBoxCollider2D second,
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

public sealed class CircleOrientedBoxNarrowPhase2D
    : ICollisionNarrowPhase2D<CircleCollider2D, OrientedBoxCollider2D>
{
    public bool TryCollide(
        CircleCollider2D circle,
        OrientedBoxCollider2D box,
        out CollisionResult2D result)
    {
        ArgumentNullException.ThrowIfNull(circle);
        ArgumentNullException.ThrowIfNull(box);
        return BoxCollisionGeometry2D.TryCollideCircle(circle, BoxGeometry2D.From(box), out result);
    }
}
