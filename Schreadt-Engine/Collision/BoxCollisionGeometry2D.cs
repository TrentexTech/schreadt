using Silk.NET.Maths;

namespace Schreadt_Engine.Collision;

internal readonly record struct BoxGeometry2D(
    Vector2D<double> Center,
    Vector2D<double> HalfSize,
    Vector2D<double> AxisX,
    Vector2D<double> AxisY)
{
    internal static BoxGeometry2D From(AxisAlignedBoxCollider2D box) =>
        AxisAligned(box.Center, box.HalfSize);

    internal static BoxGeometry2D From(OrientedBoxCollider2D box) =>
        new(box.Center, box.HalfSize, box.AxisX, box.AxisY);

    internal static BoxGeometry2D AxisAligned(Vector2D<double> center, Vector2D<double> halfSize) =>
        new(center, halfSize, Vector2D<double>.UnitX, Vector2D<double>.UnitY);
}

internal static class BoxCollisionGeometry2D
{
    internal static bool TryCollide(
        BoxGeometry2D first,
        BoxGeometry2D second,
        out CollisionResult2D result)
    {
        var centerOffset = second.Center - first.Center;
        var minimumOverlap = double.PositiveInfinity;
        var minimumAxis = Vector2D<double>.Zero;

        if (!TestAxis(first.AxisX) || !TestAxis(first.AxisY) ||
            !TestAxis(second.AxisX) || !TestAxis(second.AxisY))
        {
            result = default;
            return false;
        }

        var direction = Dot(centerOffset, minimumAxis) < 0.0 ? -minimumAxis : minimumAxis;
        result = new CollisionResult2D(direction, minimumOverlap);
        return true;

        bool TestAxis(Vector2D<double> axis)
        {
            var distance = Math.Abs(Dot(centerOffset, axis));
            var firstRadius = ProjectionRadius(first, axis);
            var secondRadius = ProjectionRadius(second, axis);
            var overlap = firstRadius + secondRadius - distance;
            if (overlap < 0.0) return false;

            if (overlap < minimumOverlap)
            {
                minimumOverlap = overlap;
                minimumAxis = axis;
            }

            return true;
        }
    }

    internal static bool TryCollideCircle(
        CircleCollider2D circle,
        BoxGeometry2D box,
        out CollisionResult2D result)
    {
        var offset = circle.Center - box.Center;
        var localCenter = new Vector2D<double>(Dot(offset, box.AxisX), Dot(offset, box.AxisY));
        var closestLocal = new Vector2D<double>(
            Math.Clamp(localCenter.X, -box.HalfSize.X, box.HalfSize.X),
            Math.Clamp(localCenter.Y, -box.HalfSize.Y, box.HalfSize.Y));
        var closestWorld = box.Center + (box.AxisX * closestLocal.X) + (box.AxisY * closestLocal.Y);
        var offsetToBox = closestWorld - circle.Center;
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

        var distanceToLeft = localCenter.X + box.HalfSize.X;
        var distanceToRight = box.HalfSize.X - localCenter.X;
        var distanceToBottom = localCenter.Y + box.HalfSize.Y;
        var distanceToTop = box.HalfSize.Y - localCenter.Y;

        var distanceToFace = distanceToLeft;
        var normal = box.AxisX;

        if (distanceToRight < distanceToFace)
        {
            distanceToFace = distanceToRight;
            normal = -box.AxisX;
        }

        if (distanceToBottom < distanceToFace)
        {
            distanceToFace = distanceToBottom;
            normal = box.AxisY;
        }

        if (distanceToTop < distanceToFace)
        {
            distanceToFace = distanceToTop;
            normal = -box.AxisY;
        }

        result = new CollisionResult2D(normal, circle.Radius + distanceToFace);
        return true;
    }

    internal static bool ContainsPoint(BoxGeometry2D box, Vector2D<double> point)
    {
        var offset = point - box.Center;
        return Math.Abs(Dot(offset, box.AxisX)) <= box.HalfSize.X &&
               Math.Abs(Dot(offset, box.AxisY)) <= box.HalfSize.Y;
    }

    internal static bool OverlapsCircle(BoxGeometry2D box, Vector2D<double> center, double radius)
    {
        var offset = center - box.Center;
        var localCenter = new Vector2D<double>(Dot(offset, box.AxisX), Dot(offset, box.AxisY));
        var closest = new Vector2D<double>(
            Math.Clamp(localCenter.X, -box.HalfSize.X, box.HalfSize.X),
            Math.Clamp(localCenter.Y, -box.HalfSize.Y, box.HalfSize.Y));
        var difference = localCenter - closest;
        return Dot(difference, difference) <= radius * radius;
    }

    private static double ProjectionRadius(BoxGeometry2D box, Vector2D<double> axis)
    {
        return box.HalfSize.X * Math.Abs(Dot(box.AxisX, axis)) +
               box.HalfSize.Y * Math.Abs(Dot(box.AxisY, axis));
    }

    private static double Dot(Vector2D<double> first, Vector2D<double> second) =>
        first.X * second.X + first.Y * second.Y;
}
