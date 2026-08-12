using Schreadt_Engine.Animation.Tweening;
using Silk.NET.Maths;

namespace Schreadt_Engine.Gui;

/// <summary>Describes an immutable, reusable visual transition between two GUI screens.</summary>
public abstract class GuiScreenTransition
{
    protected GuiScreenTransition(double duration, Func<double, double>? easing)
    {
        if (!double.IsFinite(duration) || duration <= 0.0)
            throw new ArgumentOutOfRangeException(
                nameof(duration),
                "A screen transition duration must be finite and greater than zero.");

        Duration = duration;
        Easing = easing ?? TweenEasings.Linear;
    }

    public double Duration { get; }

    public Func<double, double> Easing { get; }

    internal GuiScreenTransitionFrame CreateFrame(
        double progress,
        bool isOpening,
        Vector2D<float> viewportSize)
    {
        var easedProgress = Easing(Math.Clamp(progress, 0.0, 1.0));
        if (!double.IsFinite(easedProgress))
            throw new InvalidOperationException("The screen transition easing function returned a non-finite value.");

        return CreateFrameCore(Math.Clamp(easedProgress, 0.0, 1.0), isOpening, viewportSize);
    }

    internal abstract GuiScreenTransitionFrame CreateFrameCore(
        double progress,
        bool isOpening,
        Vector2D<float> viewportSize);
}

/// <summary>Fades through a color and switches the presented screen at the transition midpoint.</summary>
public sealed class FadeToColorScreenTransition : GuiScreenTransition
{
    public FadeToColorScreenTransition(
        Vector4D<float> color,
        double duration = 0.4,
        Func<double, double>? easing = null)
        : base(duration, easing)
    {
        EnsureFiniteColor(color);
        Color = color;
    }

    public Vector4D<float> Color { get; }

    internal override GuiScreenTransitionFrame CreateFrameCore(
        double progress,
        bool isOpening,
        Vector2D<float> viewportSize)
    {
        var showIncoming = progress >= 0.5;
        var overlayOpacity = 1.0 - Math.Abs((progress * 2.0) - 1.0);
        return new GuiScreenTransitionFrame(
            showIncoming ? 0.0f : 1.0f,
            showIncoming ? 1.0f : 0.0f,
            Vector2D<float>.Zero,
            Vector2D<float>.Zero,
            MultiplyOpacity(Color, overlayOpacity));
    }

    private static void EnsureFiniteColor(Vector4D<float> color)
    {
        if (!float.IsFinite(color.X) || !float.IsFinite(color.Y) ||
            !float.IsFinite(color.Z) || !float.IsFinite(color.W))
        {
            throw new ArgumentOutOfRangeException(nameof(color), "A transition color must be finite.");
        }
    }

    private static Vector4D<float> MultiplyOpacity(Vector4D<float> color, double opacity) =>
        new(color.X, color.Y, color.Z, color.W * (float)opacity);
}

/// <summary>Blends the outgoing and incoming GUI screens over one another.</summary>
public sealed class CrossFadeScreenTransition : GuiScreenTransition
{
    public CrossFadeScreenTransition(double duration = 0.3, Func<double, double>? easing = null)
        : base(duration, easing)
    {
    }

    internal override GuiScreenTransitionFrame CreateFrameCore(
        double progress,
        bool isOpening,
        Vector2D<float> viewportSize) =>
        new(
            (float)(1.0 - progress),
            (float)progress,
            Vector2D<float>.Zero,
            Vector2D<float>.Zero,
            Vector4D<float>.Zero);
}

public enum GuiSlideDirection
{
    Left,
    Right,
    Up,
    Down
}

/// <summary>Moves both screens across the viewport in a selected direction.</summary>
public sealed class SlideScreenTransition : GuiScreenTransition
{
    public SlideScreenTransition(
        GuiSlideDirection direction,
        double duration = 0.3,
        Func<double, double>? easing = null)
        : base(duration, easing)
    {
        if (!Enum.IsDefined(direction)) throw new ArgumentOutOfRangeException(nameof(direction));
        Direction = direction;
    }

    public GuiSlideDirection Direction { get; }

    internal override GuiScreenTransitionFrame CreateFrameCore(
        double progress,
        bool isOpening,
        Vector2D<float> viewportSize)
    {
        var direction = Direction switch
        {
            GuiSlideDirection.Left => new Vector2D<float>(-viewportSize.X, 0.0f),
            GuiSlideDirection.Right => new Vector2D<float>(viewportSize.X, 0.0f),
            GuiSlideDirection.Up => new Vector2D<float>(0.0f, -viewportSize.Y),
            GuiSlideDirection.Down => new Vector2D<float>(0.0f, viewportSize.Y),
            _ => throw new InvalidOperationException("The slide direction is invalid.")
        };

        var outgoingOffset = direction * (float)(isOpening ? -progress : progress);
        var incomingOffset = direction * (float)(isOpening ? 1.0 - progress : progress - 1.0);
        return new GuiScreenTransitionFrame(
            1.0f,
            1.0f,
            outgoingOffset,
            incomingOffset,
            Vector4D<float>.Zero);
    }
}

internal readonly record struct GuiScreenTransitionFrame(
    float OutgoingOpacity,
    float IncomingOpacity,
    Vector2D<float> OutgoingOffset,
    Vector2D<float> IncomingOffset,
    Vector4D<float> OverlayColor);
