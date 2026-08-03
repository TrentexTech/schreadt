using Schreadt_Engine.Core;
using Silk.NET.Maths;

namespace Schreadt_Engine.Gui;

public readonly record struct GuiRectangle
{
    public float X { get; }
    public float Y { get; }
    public float Width { get; }
    public float Height { get; }

    public Vector2D<float> Position => new(X, Y);
    public Vector2D<float> Size => new(Width, Height);

    public GuiRectangle(float x, float y, float width, float height)
    {
        if (!float.IsFinite(x) || !float.IsFinite(y))
            throw new ArgumentOutOfRangeException(nameof(x), "GUI rectangle positions must be finite.");
        if (!float.IsFinite(width) || !float.IsFinite(height) || width < 0.0f || height < 0.0f)
            throw new ArgumentOutOfRangeException(nameof(width), "GUI rectangle dimensions must be finite and non-negative.");

        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public GuiRectangle(Vector2D<float> position, Vector2D<float> size)
        : this(position.X, position.Y, size.X, size.Y)
    {
    }

    public bool Contains(Vector2D<float> point)
    {
        return float.IsFinite(point.X) && float.IsFinite(point.Y) &&
               point.X >= X && point.X <= X + Width &&
               point.Y >= Y && point.Y <= Y + Height;
    }
}

public interface IGuiElement
{
    bool Visible { get; set; }
    Vector2D<float> Position { get; set; }
    Vector2D<float> DesiredSize { get; }
    GuiRectangle Bounds { get; }

    void Measure(Vector2D<float> availableSize);
    void Arrange(GuiRectangle bounds);
    void Render(IRenderContext2D context);
}

public abstract class GuiElement : IGuiElement
{
    private Vector2D<float> _position;

    public bool Visible { get; set; } = true;

    public Vector2D<float> Position
    {
        get => _position;
        set
        {
            EnsureFinite(value, nameof(value));
            _position = value;
        }
    }

    public Vector2D<float> DesiredSize { get; private set; }

    public GuiRectangle Bounds { get; private set; }

    public void Measure(Vector2D<float> availableSize)
    {
        EnsureSize(availableSize, nameof(availableSize));
        var desiredSize = OnMeasure(availableSize);
        EnsureSize(desiredSize, nameof(desiredSize));
        DesiredSize = desiredSize;
    }

    public void Arrange(GuiRectangle bounds)
    {
        Bounds = bounds;
        OnArrange(bounds);
    }

    public void Render(IRenderContext2D context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (Visible) OnRender(context);
    }

    protected abstract Vector2D<float> OnMeasure(Vector2D<float> availableSize);

    protected virtual void OnArrange(GuiRectangle bounds)
    {
    }

    protected abstract void OnRender(IRenderContext2D context);

    protected static void EnsureSize(Vector2D<float> size, string parameterName)
    {
        if (!float.IsFinite(size.X) || !float.IsFinite(size.Y) || size.X < 0.0f || size.Y < 0.0f)
            throw new ArgumentOutOfRangeException(parameterName, "GUI sizes must be finite and non-negative.");
    }

    private static void EnsureFinite(Vector2D<float> value, string parameterName)
    {
        if (!float.IsFinite(value.X) || !float.IsFinite(value.Y))
            throw new ArgumentOutOfRangeException(parameterName, "GUI positions must be finite.");
    }
}
