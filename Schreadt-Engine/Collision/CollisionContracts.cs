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

    public CollisionResult2D(Vector2D<double> normal, double penetration)
    {
        if (!double.IsFinite(normal.X) || !double.IsFinite(normal.Y))
            throw new ArgumentOutOfRangeException(nameof(normal), "Collision normals must be finite.");
        if (!double.IsFinite(penetration) || penetration < 0.0)
            throw new ArgumentOutOfRangeException(nameof(penetration), "Collision penetration must be finite and non-negative.");

        var lengthSquared = normal.X * normal.X + normal.Y * normal.Y;
        if (lengthSquared <= double.Epsilon)
            throw new ArgumentOutOfRangeException(nameof(normal), "Collision normals must have a non-zero length.");

        Normal = normal / Math.Sqrt(lengthSquared);
        Penetration = penetration;
    }
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
