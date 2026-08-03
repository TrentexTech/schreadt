using Silk.NET.Maths;

namespace Schreadt_Engine.Gui;

internal sealed class PerformanceOverlay
{
    private const double SmoothingFactor = 0.1;

    private readonly GuiLabel _label;
    private double _smoothedFrameTime;

    internal PerformanceOverlay(GuiSystem gui)
    {
        var root = gui.Add(new PerformanceOverlayRoot());
        _label = root.Label;
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

    private sealed class PerformanceOverlayRoot : GuiElement
    {
        private const float Margin = 12.0f;
        private readonly GuiPanel _panel = new()
        {
            Padding = 6.0f,
            Spacing = 0.0f
        };

        internal GuiLabel Label { get; }

        internal PerformanceOverlayRoot()
        {
            Label = _panel.AddLabel("FPS: --\nFRAME: -- MS");
        }

        protected override Vector2D<float> OnMeasure(Vector2D<float> availableSize)
        {
            _panel.Measure(availableSize);
            // Occupying the logical viewport lets this root calculate a true
            // right edge for every aspect ratio and window size.
            return availableSize;
        }

        protected override void OnArrange(GuiRectangle bounds)
        {
            var x = bounds.X + Math.Max(0.0f, bounds.Width - _panel.DesiredSize.X - Margin);
            var y = bounds.Y + Margin;
            _panel.Arrange(new GuiRectangle(
                new Vector2D<float>(x, y),
                _panel.DesiredSize));
        }

        protected override void OnRender(Schreadt_Engine.Core.IRenderContext2D context)
        {
            _panel.Render(context);
        }
    }
}
