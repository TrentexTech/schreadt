namespace Schreadt_Engine.Collision;

public sealed class CircleBoxNarrowPhase2D
    : ICollisionNarrowPhase2D<CircleCollider2D, AxisAlignedBoxCollider2D>
{
    public bool TryCollide(
        CircleCollider2D circle,
        AxisAlignedBoxCollider2D box,
        out CollisionResult2D result)
    {
        ArgumentNullException.ThrowIfNull(circle);
        ArgumentNullException.ThrowIfNull(box);

        return BoxCollisionGeometry2D.TryCollideCircle(circle, BoxGeometry2D.From(box), out result);
    }
}
