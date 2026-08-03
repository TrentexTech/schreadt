using Silk.NET.Maths;
using Schreadt_Engine.Collision;
using Schreadt_Engine.Core;

namespace Schreadt_Engine.Gui;

internal sealed class PerformanceOverlay
{
    private const double SmoothingFactor = 0.1;

    private readonly GuiLabel _label;
    private double _smoothedFrameTime;
    private double _rangeElapsed;
    private double _minimumFrameTime = double.PositiveInfinity;
    private double _maximumFrameTime;
    private double _memorySampleElapsed = 1.0;
    private double _memoryMegabytes;
    private int _generationZeroCollections;
    private int _generationOneCollections;
    private int _generationTwoCollections;

    internal PerformanceOverlay(GuiSystem gui)
    {
        var root = gui.Add(new PerformanceOverlayRoot());
        _label = root.Label;
        _label.Scale = 2.0f;
        _label.Padding = 0.0f;
        _label.BackgroundColor = Vector4D<float>.Zero;
    }

    internal void Update(
        double frameTime,
        RuntimeController runtime,
        int fixedStepCount,
        CollisionStatistics2D collisions,
        RenderStatistics rendering,
        Vector2D<int> viewportSize)
    {
        if (!double.IsFinite(frameTime) || frameTime <= 0) return;
        ArgumentNullException.ThrowIfNull(runtime);

        _smoothedFrameTime = _smoothedFrameTime <= 0
            ? frameTime
            : _smoothedFrameTime + (frameTime - _smoothedFrameTime) * SmoothingFactor;
        _rangeElapsed += frameTime;
        _minimumFrameTime = Math.Min(_minimumFrameTime, frameTime);
        _maximumFrameTime = Math.Max(_maximumFrameTime, frameTime);
        _memorySampleElapsed += frameTime;
        if (_memorySampleElapsed >= 1.0)
        {
            _memorySampleElapsed %= 1.0;
            _memoryMegabytes = GC.GetTotalMemory(forceFullCollection: false) / (1024.0 * 1024.0);
            _generationZeroCollections = GC.CollectionCount(0);
            _generationOneCollections = GC.CollectionCount(1);
            _generationTwoCollections = GC.CollectionCount(2);
        }

        var framesPerSecond = 1.0 / _smoothedFrameTime;
        var milliseconds = _smoothedFrameTime * 1000.0;
        var minimumMilliseconds = _minimumFrameTime * 1000.0;
        var maximumMilliseconds = _maximumFrameTime * 1000.0;
        var simulationState = runtime.IsPaused ? "PAUSED" : "RUNNING";
        _label.Text = FormattableString.Invariant($"""
            FPS: {framesPerSecond:F1}
            FRAME: {milliseconds:F2} MS  RANGE: {minimumMilliseconds:F2}-{maximumMilliseconds:F2}
            DRAW: {rendering.DrawCallCount}  PRIM: {rendering.PrimitiveCount}  VERT: {rendering.VertexCount}
            SIM: {simulationState}  FIXED: {fixedStepCount}  SCALE: {runtime.TimeScale:F2}
            PHYS: {collisions.ActiveColliderCount}/{collisions.RegisteredColliderCount}  CONTACT: {collisions.ContactCount}
            CHECKS: {collisions.PairCheckCount} PAIR  {collisions.NarrowPhaseTestCount} NARROW
            VIEW: {viewportSize.X}X{viewportSize.Y}
            MEM: {_memoryMegabytes:F1} MB  GC: {_generationZeroCollections}/{_generationOneCollections}/{_generationTwoCollections}
            """);

        if (_rangeElapsed < 1.0) return;
        _rangeElapsed %= 1.0;
        _minimumFrameTime = double.PositiveInfinity;
        _maximumFrameTime = 0.0;
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
            Label = _panel.AddLabel(
                "FPS: --\nFRAME: -- MS  RANGE: --\nDRAW: --  PRIM: --  VERT: --\n" +
                "SIM: --  FIXED: --  SCALE: --\nPHYS: --  CONTACT: --\nCHECKS: --\n" +
                "VIEW: --\nMEM: --  GC: --");
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
