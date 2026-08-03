using Silk.NET.Maths;

namespace Schreadt_Engine.Core;

internal static class ConvexPolygon2D
{
    private const double CollinearityTolerance = 1e-10;

    internal static Vector2D<double>[] CopyAndValidate(
        IEnumerable<Vector2D<double>> vertices,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(vertices);
        var copy = vertices.ToArray();
        Validate(copy, parameterName);
        return copy;
    }

    internal static void Validate(IReadOnlyList<Vector2D<double>> vertices, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(vertices);
        if (vertices.Count < 3)
            throw new ArgumentException("A polygon requires at least three vertices.", parameterName);

        for (var firstIndex = 0; firstIndex < vertices.Count; firstIndex++)
        {
            EnsureFinite(vertices[firstIndex], parameterName);
            for (var secondIndex = firstIndex + 1; secondIndex < vertices.Count; secondIndex++)
            {
                var difference = vertices[secondIndex] - vertices[firstIndex];
                if (difference.X * difference.X + difference.Y * difference.Y <= CollinearityTolerance * CollinearityTolerance)
                    throw new ArgumentException("A polygon cannot contain duplicate vertices.", parameterName);
            }
        }

        double? winding = null;
        for (var index = 0; index < vertices.Count; index++)
        {
            var first = vertices[index];
            var second = vertices[(index + 1) % vertices.Count];
            var third = vertices[(index + 2) % vertices.Count];

            var cross = Cross(second - first, third - second);
            if (Math.Abs(cross) <= CollinearityTolerance)
                throw new ArgumentException("A polygon cannot contain duplicate or collinear consecutive vertices.", parameterName);

            var currentWinding = Math.Sign(cross);
            if (winding is null) winding = currentWinding;
            else if (currentWinding != winding)
                throw new ArgumentException("Only convex polygons are supported.", parameterName);
        }

        for (var firstEdge = 0; firstEdge < vertices.Count; firstEdge++)
        {
            var firstEdgeEnd = (firstEdge + 1) % vertices.Count;
            for (var secondEdge = firstEdge + 1; secondEdge < vertices.Count; secondEdge++)
            {
                var secondEdgeEnd = (secondEdge + 1) % vertices.Count;
                if (firstEdge == secondEdge || firstEdgeEnd == secondEdge || secondEdgeEnd == firstEdge) continue;

                if (SegmentsIntersect(
                        vertices[firstEdge],
                        vertices[firstEdgeEnd],
                        vertices[secondEdge],
                        vertices[secondEdgeEnd]))
                    throw new ArgumentException("A polygon cannot contain intersecting edges.", parameterName);
            }
        }
    }

    private static double Cross(Vector2D<double> first, Vector2D<double> second)
    {
        return first.X * second.Y - first.Y * second.X;
    }

    private static bool SegmentsIntersect(
        Vector2D<double> firstStart,
        Vector2D<double> firstEnd,
        Vector2D<double> secondStart,
        Vector2D<double> secondEnd)
    {
        var firstSide = Cross(firstEnd - firstStart, secondStart - firstStart);
        var secondSide = Cross(firstEnd - firstStart, secondEnd - firstStart);
        var thirdSide = Cross(secondEnd - secondStart, firstStart - secondStart);
        var fourthSide = Cross(secondEnd - secondStart, firstEnd - secondStart);

        return Math.Sign(firstSide) != Math.Sign(secondSide) && Math.Sign(thirdSide) != Math.Sign(fourthSide);
    }

    private static void EnsureFinite(Vector2D<double> vertex, string parameterName)
    {
        if (!double.IsFinite(vertex.X) || !double.IsFinite(vertex.Y))
            throw new ArgumentOutOfRangeException(parameterName, "Polygon vertices must be finite.");
    }
}
