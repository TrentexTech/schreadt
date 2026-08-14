using Schreadt_Engine.Component;
using Silk.NET.Maths;

namespace Schreadt_Engine.Collision;

/// <summary>
/// A rectangular collider whose physical orientation follows its owner's world rotation.
/// Transform scale is intentionally ignored by physics geometry.
/// </summary>
public sealed class OrientedBoxCollider2D : Collider2D
{
    private Vector2D<double> _size;
    private Vector2D<double> _offset;
    private double _rotationOffset;

    public OrientedBoxCollider2D(Vector2D<double> size)
    {
        Size = size;
    }

    /// <summary>The full local width and height of the box.</summary>
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

    /// <summary>Counter-clockwise local rotation in radians relative to the owner.</summary>
    public double RotationOffset
    {
        get => _rotationOffset;
        set
        {
            if (!double.IsFinite(value))
                throw new ArgumentOutOfRangeException(nameof(value), "Collider rotation must be finite.");

            _rotationOffset = value;
        }
    }

    public double WorldRotation => Owner.Transform.WorldRotation + RotationOffset;

    public Vector2D<double> AxisX
    {
        get
        {
            var rotation = WorldRotation;
            return new Vector2D<double>(Math.Cos(rotation), Math.Sin(rotation));
        }
    }

    public Vector2D<double> AxisY
    {
        get
        {
            var rotation = WorldRotation;
            return new Vector2D<double>(-Math.Sin(rotation), Math.Cos(rotation));
        }
    }

    public override Vector2D<double> Center =>
        Owner.Position + Transform2D.Rotate(Offset, Owner.Transform.WorldRotation);

    /// <summary>Returns a new array containing the four world-space corners in counter-clockwise order.</summary>
    public Vector2D<double>[] GetWorldVertices()
    {
        var horizontal = AxisX * HalfSize.X;
        var vertical = AxisY * HalfSize.Y;
        return
        [
            Center - horizontal - vertical,
            Center + horizontal - vertical,
            Center + horizontal + vertical,
            Center - horizontal + vertical
        ];
    }
}
