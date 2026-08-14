using Silk.NET.Maths;

namespace Schreadt_Engine.Collision;

/// <summary>
/// Exposes the world-space anchor shared by two-dimensional collision shapes.
/// </summary>
public interface ICollisionShape2D
{
    Vector2D<double> Center { get; }
}

/// <summary>
/// The geometric result of a narrow-phase collision test. The normal points
/// from the first shape toward the second shape.
/// </summary>
public readonly record struct CollisionResult2D
{
    public Vector2D<double> Normal { get; }
    public double Penetration { get; }
    public int ContactPointCount { get; }
    private Vector2D<double> FirstContactPoint { get; }
    private Vector2D<double> SecondContactPoint { get; }

    public CollisionResult2D(Vector2D<double> normal, double penetration)
        : this(normal, penetration, 0, default, default)
    {
    }

    public CollisionResult2D(
        Vector2D<double> normal,
        double penetration,
        Vector2D<double> contactPoint)
        : this(normal, penetration, 1, contactPoint, default)
    {
    }

    public CollisionResult2D(
        Vector2D<double> normal,
        double penetration,
        Vector2D<double> firstContactPoint,
        Vector2D<double> secondContactPoint)
        : this(normal, penetration, 2, firstContactPoint, secondContactPoint)
    {
    }

    private CollisionResult2D(
        Vector2D<double> normal,
        double penetration,
        int contactPointCount,
        Vector2D<double> firstContactPoint,
        Vector2D<double> secondContactPoint)
    {
        if (!double.IsFinite(normal.X) || !double.IsFinite(normal.Y))
            throw new ArgumentOutOfRangeException(nameof(normal), "Collision normals must be finite.");
        if (!double.IsFinite(penetration) || penetration < 0.0)
            throw new ArgumentOutOfRangeException(nameof(penetration), "Collision penetration must be finite and non-negative.");
        if (contactPointCount >= 1 && !IsFinite(firstContactPoint))
            throw new ArgumentOutOfRangeException(nameof(firstContactPoint), "Collision contact points must be finite.");
        if (contactPointCount >= 2 && !IsFinite(secondContactPoint))
            throw new ArgumentOutOfRangeException(nameof(secondContactPoint), "Collision contact points must be finite.");

        var lengthSquared = normal.X * normal.X + normal.Y * normal.Y;
        if (lengthSquared <= double.Epsilon)
            throw new ArgumentOutOfRangeException(nameof(normal), "Collision normals must have a non-zero length.");

        Normal = normal / Math.Sqrt(lengthSquared);
        Penetration = penetration;
        ContactPointCount = contactPointCount;
        FirstContactPoint = firstContactPoint;
        SecondContactPoint = secondContactPoint;
    }

    public Vector2D<double> GetContactPoint(int index)
    {
        return index switch
        {
            0 when ContactPointCount >= 1 => FirstContactPoint,
            1 when ContactPointCount >= 2 => SecondContactPoint,
            _ => throw new ArgumentOutOfRangeException(nameof(index))
        };
    }

    private static bool IsFinite(Vector2D<double> point) =>
        double.IsFinite(point.X) && double.IsFinite(point.Y);
}

/// <summary>
/// Tests one ordered pair of collision shape types. Implementations should
/// return a normal pointing from <paramref name="first"/> toward
/// <paramref name="second"/>.
/// </summary>
public interface ICollisionNarrowPhase2D<in TFirst, in TSecond>
    where TFirst : ICollisionShape2D
    where TSecond : ICollisionShape2D
{
    bool TryCollide(TFirst first, TSecond second, out CollisionResult2D result);
}
