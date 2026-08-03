using Silk.NET.Maths;

namespace Schreadt_Engine.Gui;

internal sealed class PerformanceOverlay
{
    private const double SmoothingFactor = 0.1;

    private readonly GuiLabel _label;
    private double _smoothedFrameTime;

    internal PerformanceOverlay(GuiSystem gui)
    {
        var panel = gui.AddPanel();
        panel.Position = new Vector2D<float>(12.0f, 12.0f);
        panel.Padding = 6.0f;
        panel.Spacing = 0.0f;

        _label = panel.AddLabel("FPS: --\nFRAME: -- MS");
        _label.Scale = 2.0f;
        _label.Padding = 0.0f;
        _label.BackgroundColor = Vector4D<float>.Zero;
    }

    internal void Update(double frameTime)
    {
        if (!double.IsFinite(frameTime) || frameTime <= 0) return;

        _smoothedFrameTime = _smoothedFrameTime <= 0
            ? frameTime
            : _smoothedFrameTime + (frameTime - _smoothedFrameTime) * SmoothingFactor;

        var framesPerSecond = 1.0 / _smoothedFrameTime;
        var milliseconds = _smoothedFrameTime * 1000.0;
        _label.Text = FormattableString.Invariant($"FPS: {framesPerSecond:F1}\nFRAME: {milliseconds:F2} MS");
    }
}
