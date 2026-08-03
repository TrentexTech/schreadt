using Silk.NET.Maths;

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

        var minimum = box.Minimum;
        var maximum = box.Maximum;
        var closestPoint = new Vector2D<double>(
            Math.Clamp(circle.Center.X, minimum.X, maximum.X),
            Math.Clamp(circle.Center.Y, minimum.Y, maximum.Y));
        var offsetToBox = closestPoint - circle.Center;
        var distanceSquared = Dot(offsetToBox, offsetToBox);

        if (distanceSquared > circle.Radius * circle.Radius)
        {
            result = default;
            return false;
        }

        if (distanceSquared > double.Epsilon)
        {
            var distance = Math.Sqrt(distanceSquared);
            result = new CollisionResult2D(offsetToBox / distance, circle.Radius - distance);
            return true;
        }

        // The circle center is inside the box. Use the nearest face and include
        // the radius so position correction moves the entire circle outside.
        var distanceToLeft = circle.Center.X - minimum.X;
        var distanceToRight = maximum.X - circle.Center.X;
        var distanceToBottom = circle.Center.Y - minimum.Y;
        var distanceToTop = maximum.Y - circle.Center.Y;

        var distanceToFace = distanceToLeft;
        var normal = new Vector2D<double>(1.0, 0.0);

        if (distanceToRight < distanceToFace)
        {
            distanceToFace = distanceToRight;
            normal = new Vector2D<double>(-1.0, 0.0);
        }

        if (distanceToBottom < distanceToFace)
        {
            distanceToFace = distanceToBottom;
            normal = new Vector2D<double>(0.0, 1.0);
        }

        if (distanceToTop < distanceToFace)
        {
            distanceToFace = distanceToTop;
            normal = new Vector2D<double>(0.0, -1.0);
        }

        result = new CollisionResult2D(normal, circle.Radius + distanceToFace);
        return true;
    }

    private static double Dot(Vector2D<double> first, Vector2D<double> second)
    {
        return first.X * second.X + first.Y * second.Y;
    }
}
