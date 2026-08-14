using Silk.NET.Maths;

namespace Schreadt_Engine.Collision;

public static class PhysicsInertia2D
{
    public static double ForSolidCircle(double mass, double radius)
    {
        ValidateMass(mass);
        if (!double.IsFinite(radius) || radius <= 0.0)
            throw new ArgumentOutOfRangeException(nameof(radius), "Radius must be finite and greater than zero.");

        return 0.5 * mass * radius * radius;
    }

    public static double ForSolidBox(double mass, Vector2D<double> size)
    {
        ValidateMass(mass);
        if (!double.IsFinite(size.X) || !double.IsFinite(size.Y) || size.X <= 0.0 || size.Y <= 0.0)
            throw new ArgumentOutOfRangeException(nameof(size), "Box size must be finite and greater than zero.");

        return mass * (size.X * size.X + size.Y * size.Y) / 12.0;
    }

    public static double AtOffset(double centroidMomentOfInertia, double mass, Vector2D<double> offset)
    {
        if (!double.IsFinite(centroidMomentOfInertia) || centroidMomentOfInertia <= 0.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(centroidMomentOfInertia),
                "Centroid moment of inertia must be finite and greater than zero.");
        }

        ValidateMass(mass);
        if (!double.IsFinite(offset.X) || !double.IsFinite(offset.Y))
            throw new ArgumentOutOfRangeException(nameof(offset), "Offset must be finite.");

        return centroidMomentOfInertia + mass * (offset.X * offset.X + offset.Y * offset.Y);
    }

    private static void ValidateMass(double mass)
    {
        if (!double.IsFinite(mass) || mass <= 0.0)
            throw new ArgumentOutOfRangeException(nameof(mass), "Mass must be finite and greater than zero.");
    }
}
