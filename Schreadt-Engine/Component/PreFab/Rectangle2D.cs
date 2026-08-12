using Schreadt_Engine.Component.Logic;
using Schreadt_Engine.Core;
using Silk.NET.Maths;

namespace Schreadt_Engine.Component.PreFab;

public class Rectangle2D : Actor
{
    private Vector2D<double> _size = new(0.7, 0.7);
    private double _rotationRadians;

    public Vector2D<double> Size
    {
        get => _size;
        set
        {
            if (!double.IsFinite(value.X) || !double.IsFinite(value.Y) || value.X <= 0.0 || value.Y <= 0.0)
                throw new ArgumentOutOfRangeException(nameof(value), "Rectangle size must be finite and positive.");
            _size = value;
        }
    }

    public double RotationRadians
    {
        get => _rotationRadians;
        set
        {
            if (!double.IsFinite(value))
                throw new ArgumentOutOfRangeException(nameof(value), "Rectangle rotation must be finite.");
            _rotationRadians = value;
        }
    }

    public Vector4D<float> Color { get; set; } = new(0.95f, 0.45f, 0.2f, 1.0f);

    public Rectangle2D()
    {
    }

    protected override void OnRender(IRenderContext2D renderer)
    {
        renderer.DrawRectangle(Position, Size, Color, RotationRadians);
    }
}