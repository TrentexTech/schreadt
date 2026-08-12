using Schreadt_Engine.Component;
using Silk.NET.Maths;

namespace Schreadt_Engine.Collision;

/// <summary>
/// A rectangular collider whose edges remain aligned with the world axes.
/// </summary>
public sealed class AxisAlignedBoxCollider2D : Collider2D
{
    private Vector2D<double> _size;
    private Vector2D<double> _offset;

    public AxisAlignedBoxCollider2D(Vector2D<double> size)
    {
        Size = size;
    }

    /// <summary>The full width and height of the box.</summary>
    public Vector2D<double> Size
    {
        get => _size;
        set
        {
            if (!double.IsFinite(value.X) || !double.IsFinite(value.Y) || value.X <= 0.0 || value.Y <= 0.0)
                throw new ArgumentOutOfRangeException(nameof(value), "Collider size must be finite and greater than zero.");

            _size = value;
        }
    }

    public Vector2D<double> HalfSize => Size * 0.5;

    public Vector2D<double> Offset
    {
        get => _offset;
        set
        {
            if (!double.IsFinite(value.X) || !double.IsFinite(value.Y))
                throw new ArgumentOutOfRangeException(nameof(value), "Collider offset must be finite.");

            _offset = value;
        }
    }

    /// <summary>
    /// World-space center. Owner and ancestor rotation affect the offset, but this collider
    /// remains axis-aligned and transform scale is intentionally ignored by physics geometry.
    /// </summary>
    public override Vector2D<double> Center =>
        Owner.Position + Transform2D.Rotate(Offset, Owner.Transform.WorldRotation);

    public Vector2D<double> Minimum => Center - HalfSize;

    public Vector2D<double> Maximum => Center + HalfSize;
}
