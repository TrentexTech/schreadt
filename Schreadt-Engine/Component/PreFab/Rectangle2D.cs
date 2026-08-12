using Schreadt_Engine.Component.Logic;
using Schreadt_Engine.Core;
using Silk.NET.Maths;

namespace Schreadt_Engine.Component.PreFab;

public class Rectangle2D : Actor
{
    private Vector2D<double> _size = new(0.7, 0.7);

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
        get => Transform.WorldRotation;
        set => Transform.SetWorldRotation(value);
    }

    public Vector4D<float> Color { get; set; } = new(0.95f, 0.45f, 0.2f, 1.0f);

    public Rectangle2D()
    {
    }

    protected override void OnRender(IRenderContext2D renderer)
    {
        var worldScale = Transform.WorldScale;
        renderer.DrawRectangle(
            Position,
            new Vector2D<double>(Size.X * worldScale.X, Size.Y * worldScale.Y),
            Color,
            RotationRadians);
    }
}
