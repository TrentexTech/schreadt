using Schreadt_Engine.Animation.Tweening;
using Schreadt_Engine.Gui;
using Silk.NET.Maths;

namespace Schreadt_Engine.Core;

/// <summary>Describes an immutable, reusable visual transition between complete scenes.</summary>
public abstract class SceneTransition
{
    protected SceneTransition(double duration, Func<double, double>? easing)
    {
        if (!double.IsFinite(duration) || duration <= 0.0)
            throw new ArgumentOutOfRangeException(
                nameof(duration),
                "A scene transition duration must be finite and greater than zero.");

        Duration = duration;
        Easing = easing ?? TweenEasings.Linear;
    }

    public double Duration { get; }

    public Func<double, double> Easing { get; }

    internal double PhaseDuration => Duration * 0.5;

    internal Vector4D<float> CreateOverlayColor(double progress)
    {
        var clampedProgress = Math.Clamp(progress, 0.0, 1.0);
        var phaseProgress = clampedProgress <= 0.5
            ? clampedProgress * 2.0
            : (clampedProgress - 0.5) * 2.0;
        var easedProgress = Easing(phaseProgress);
        if (!double.IsFinite(easedProgress))
            throw new InvalidOperationException("The scene transition easing function returned a non-finite value.");

        var opacity = clampedProgress <= 0.5
            ? Math.Clamp(easedProgress, 0.0, 1.0)
            : 1.0 - Math.Clamp(easedProgress, 0.0, 1.0);
        return CreateOverlayColorCore(opacity);
    }

    internal abstract Vector4D<float> CreateOverlayColorCore(double opacity);
}

/// <summary>Fades to a color, switches scenes while covered, and fades back out.</summary>
public sealed class FadeToColorSceneTransition : SceneTransition
{
    public FadeToColorSceneTransition(
        Vector4D<float> color,
        double duration = 0.6,
        Func<double, double>? easing = null)
        : base(duration, easing)
    {
        if (!float.IsFinite(color.X) || !float.IsFinite(color.Y) ||
            !float.IsFinite(color.Z) || !float.IsFinite(color.W))
        {
            throw new ArgumentOutOfRangeException(nameof(color), "A scene transition color must be finite.");
        }

        Color = color;
    }

    public Vector4D<float> Color { get; }

    internal override Vector4D<float> CreateOverlayColorCore(double opacity) =>
        new(Color.X, Color.Y, Color.Z, Color.W * (float)opacity);
}

internal sealed class SceneTransitionOverlay : GuiElement
{
    internal Vector4D<float> Color { get; set; }

    protected override Vector2D<float> OnMeasure(Vector2D<float> availableSize) => availableSize;

    protected override void OnRender(IRenderContext2D context)
    {
        if (Color.W > 0.0f) context.DrawScreenRectangle(Bounds.Position, Bounds.Size, Color);
    }
}
