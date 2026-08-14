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
        var contactPoint = CalculateContactPoint(first, second, direction);
        result = new CollisionResult2D(direction, minimumOverlap, contactPoint);
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
            var outsideNormal = offsetToBox / distance;
            var circleSurface = circle.Center + outsideNormal * circle.Radius;
            result = new CollisionResult2D(
                outsideNormal,
                circle.Radius - distance,
                (circleSurface + closestWorld) * 0.5);
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

        var boxSurface = circle.Center - normal * distanceToFace;
        var circleSurfaceInside = circle.Center - normal * circle.Radius;
        result = new CollisionResult2D(
            normal,
            circle.Radius + distanceToFace,
            (boxSurface + circleSurfaceInside) * 0.5);
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

    private static Vector2D<double> CalculateContactPoint(
        BoxGeometry2D first,
        BoxGeometry2D second,
        Vector2D<double> normal)
    {
        Span<Vector2D<double>> firstFeature = stackalloc Vector2D<double>[2];
        Span<Vector2D<double>> secondFeature = stackalloc Vector2D<double>[2];
        var firstFeatureCount = GetSupportFeature(first, normal, firstFeature);
        var secondFeatureCount = GetSupportFeature(second, -normal, secondFeature);

        if (firstFeatureCount == 1)
        {
            var secondPoint = secondFeatureCount == 1
                ? secondFeature[0]
                : ClosestPointOnSegment(firstFeature[0], secondFeature[0], secondFeature[1]);
            return (firstFeature[0] + secondPoint) * 0.5;
        }

        if (secondFeatureCount == 1)
        {
            var firstPoint = ClosestPointOnSegment(secondFeature[0], firstFeature[0], firstFeature[1]);
            return (firstPoint + secondFeature[0]) * 0.5;
        }

        var tangent = new Vector2D<double>(-normal.Y, normal.X);
        var firstNormalMaximum = Dot(firstFeature[0], normal);
        var secondNormalMinimum = Dot(secondFeature[0], normal);
        var normalCoordinate = (firstNormalMaximum + secondNormalMinimum) * 0.5;
        var tangentMinimum = Math.Max(
            Math.Min(Dot(firstFeature[0], tangent), Dot(firstFeature[1], tangent)),
            Math.Min(Dot(secondFeature[0], tangent), Dot(secondFeature[1], tangent)));
        var tangentMaximum = Math.Min(
            Math.Max(Dot(firstFeature[0], tangent), Dot(firstFeature[1], tangent)),
            Math.Max(Dot(secondFeature[0], tangent), Dot(secondFeature[1], tangent)));
        var tangentCoordinate = (tangentMinimum + tangentMaximum) * 0.5;

        return normal * normalCoordinate + tangent * tangentCoordinate;
    }

    private static int GetSupportFeature(
        BoxGeometry2D box,
        Vector2D<double> direction,
        Span<Vector2D<double>> feature)
    {
        Span<Vector2D<double>> vertices = stackalloc Vector2D<double>[4];
        var horizontal = box.AxisX * box.HalfSize.X;
        var vertical = box.AxisY * box.HalfSize.Y;
        vertices[0] = box.Center - horizontal - vertical;
        vertices[1] = box.Center + horizontal - vertical;
        vertices[2] = box.Center + horizontal + vertical;
        vertices[3] = box.Center - horizontal + vertical;

        var maximumProjection = double.NegativeInfinity;
        for (var index = 0; index < vertices.Length; index++)
            maximumProjection = Math.Max(maximumProjection, Dot(vertices[index], direction));

        var count = 0;
        for (var index = 0; index < vertices.Length && count < feature.Length; index++)
        {
            if (maximumProjection - Dot(vertices[index], direction) <= 1e-10)
                feature[count++] = vertices[index];
        }

        return count;
    }

    private static Vector2D<double> ClosestPointOnSegment(
        Vector2D<double> point,
        Vector2D<double> start,
        Vector2D<double> end)
    {
        var segment = end - start;
        var lengthSquared = Dot(segment, segment);
        if (lengthSquared <= double.Epsilon) return start;

        var amount = Math.Clamp(Dot(point - start, segment) / lengthSquared, 0.0, 1.0);
        return start + segment * amount;
    }

    private static double ProjectionRadius(BoxGeometry2D box, Vector2D<double> axis)
    {
        return box.HalfSize.X * Math.Abs(Dot(box.AxisX, axis)) +
               box.HalfSize.Y * Math.Abs(Dot(box.AxisY, axis));
    }

    private static double Dot(Vector2D<double> first, Vector2D<double> second) =>
        first.X * second.X + first.Y * second.Y;
}
