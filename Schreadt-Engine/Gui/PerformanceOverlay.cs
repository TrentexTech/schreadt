using Silk.NET.Maths;
using Schreadt_Engine.Collision;
using Schreadt_Engine.Core;

namespace Schreadt_Engine.Gui;

internal readonly record struct PerformanceDisplayMetrics(
    Vector2D<int> WindowSize,
    WindowDisplayState WindowState,
    bool VSync,
    Vector2D<int> FramebufferSize,
    Vector2D<float> FramebufferScale,
    Vector2D<int> ViewportOffset,
    Vector2D<int> ViewportSize,
    Vector2D<float> GuiLogicalSize,
    float GuiScale,
    float GuiReferenceHeight)
{
    internal static PerformanceDisplayMetrics Create(
        Vector2D<int> windowSize,
        WindowDisplayState windowState,
        bool vSync,
        Vector2D<int> framebufferSize,
        Vector2D<int> viewportOffset,
        Vector2D<int> viewportSize,
        GuiSystem gui)
    {
        ArgumentNullException.ThrowIfNull(gui);
        var safeWindowWidth = Math.Max(1, windowSize.X);
        var safeWindowHeight = Math.Max(1, windowSize.Y);
        var guiScale = gui.GetRenderScale(viewportSize);
        return new PerformanceDisplayMetrics(
            windowSize,
            windowState,
            vSync,
            framebufferSize,
            new Vector2D<float>(
                framebufferSize.X / (float)safeWindowWidth,
                framebufferSize.Y / (float)safeWindowHeight),
            viewportOffset,
            viewportSize,
            new Vector2D<float>(viewportSize.X / guiScale, viewportSize.Y / guiScale),
            guiScale,
            gui.ReferenceHeight);
    }
}

internal sealed class PerformanceOverlay
{
    private const double SmoothingFactor = 0.1;
    internal const InputKey ToggleKey = InputKey.F3;

    private readonly PerformanceOverlayRoot _root;
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
        _root = gui.Add(new PerformanceOverlayRoot());
    }

    internal bool IsVisible => _root.Visible;

    internal void HandleInput(IInputState input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!input.WasKeyPressed(ToggleKey)) return;

        _root.Visible = !_root.Visible;
        if (_root.Visible)
        {
            _smoothedFrameTime = 0.0;
            _rangeElapsed = 0.0;
            _minimumFrameTime = double.PositiveInfinity;
            _maximumFrameTime = 0.0;
        }

        EngineLog.Debug($"Performance overlay {(_root.Visible ? "shown" : "hidden")} with {ToggleKey}.", "GUI");
    }

    internal void Update(
        double frameTime,
        RuntimeController runtime,
        int fixedStepCount,
        CollisionStatistics2D collisions,
        RenderStatistics rendering,
        PerformanceDisplayMetrics display)
    {
        if (!IsVisible) return;
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
        _root.Frame.Text = FormattableString.Invariant($"""
            FPS: {framesPerSecond:F1}
            TIME: {milliseconds:F2} MS  RANGE: {minimumMilliseconds:F2}-{maximumMilliseconds:F2}
            """);
        _root.Rendering.Text = FormattableString.Invariant($"""
            DRAW: {rendering.DrawCallCount}  PRIM: {rendering.PrimitiveCount}  VERT: {rendering.VertexCount}
            UPLOAD: {rendering.TextureUploadCount}  DATA: {FormatBytes(rendering.TextureUploadByteCount)}
            """);
        _root.Simulation.Text = FormattableString.Invariant(
            $"SIM: {simulationState}  FIXED: {fixedStepCount}  SCALE: {runtime.TimeScale:F2}");
        _root.Physics.Text = FormattableString.Invariant($"""
            PHYS: {collisions.ActiveColliderCount}/{collisions.RegisteredColliderCount}  CONTACT: {collisions.ContactCount}
            CHECKS: {collisions.PairCheckCount} PAIR  {collisions.NarrowPhaseTestCount} NARROW
            """);
        _root.Display.Text = FormattableString.Invariant($"""
            WINDOW: {display.WindowSize.X}X{display.WindowSize.Y}  {display.WindowState.ToString().ToUpperInvariant()}  VSYNC: {(display.VSync ? "ON" : "OFF")}
            FRAMEBUFFER: {display.FramebufferSize.X}X{display.FramebufferSize.Y}  SCALE: {display.FramebufferScale.X:F2}X/{display.FramebufferScale.Y:F2}X
            VIEWPORT: {display.ViewportSize.X}X{display.ViewportSize.Y}  OFFSET: {display.ViewportOffset.X},{display.ViewportOffset.Y}
            """);
        _root.Gui.Text = FormattableString.Invariant(
            $"LOGICAL: {display.GuiLogicalSize.X:F1}X{display.GuiLogicalSize.Y:F1}  SCALE: {display.GuiScale:F2}X  REF: {display.GuiReferenceHeight:F1}");
        _root.Memory.Text = FormattableString.Invariant(
            $"MEM: {_memoryMegabytes:F1} MB  GC: {_generationZeroCollections}/{_generationOneCollections}/{_generationTwoCollections}");

        if (_rangeElapsed < 1.0) return;
        _rangeElapsed %= 1.0;
        _minimumFrameTime = double.PositiveInfinity;
        _maximumFrameTime = 0.0;
    }

    private static string FormatBytes(long byteCount)
    {
        if (byteCount < 1024) return FormattableString.Invariant($"{byteCount} B");
        if (byteCount < 1024 * 1024) return FormattableString.Invariant($"{byteCount / 1024.0:F1} KB");
        return FormattableString.Invariant($"{byteCount / (1024.0 * 1024.0):F2} MB");
    }

    private sealed class PerformanceOverlayRoot : GuiElement
    {
        private const float Margin = 12.0f;
        private const float TextScale = 1.75f;
        private static readonly Vector4D<float> Transparent = Vector4D<float>.Zero;
        private static readonly Vector4D<float> FrameColor = new(0.35f, 0.93f, 1.0f, 1.0f);
        private static readonly Vector4D<float> RenderingColor = new(0.42f, 0.68f, 1.0f, 1.0f);
        private static readonly Vector4D<float> SimulationColor = new(0.45f, 1.0f, 0.58f, 1.0f);
        private static readonly Vector4D<float> PhysicsColor = new(1.0f, 0.68f, 0.28f, 1.0f);
        private static readonly Vector4D<float> DisplayColor = new(0.78f, 0.58f, 1.0f, 1.0f);
        private static readonly Vector4D<float> GuiColor = new(1.0f, 0.48f, 0.76f, 1.0f);
        private static readonly Vector4D<float> MemoryColor = new(1.0f, 0.86f, 0.32f, 1.0f);
        private readonly GuiPanel _panel = new()
        {
            Padding = 5.0f,
            Spacing = 5.0f
        };

        internal GuiLabel Frame { get; }
        internal GuiLabel Rendering { get; }
        internal GuiLabel Simulation { get; }
        internal GuiLabel Physics { get; }
        internal GuiLabel Display { get; }
        internal GuiLabel Gui { get; }
        internal GuiLabel Memory { get; }

        internal PerformanceOverlayRoot()
        {
            var title = _panel.AddLabel("PERFORMANCE OVERLAY  [F3: HIDE]");
            ConfigureLabel(title, FrameColor);

            Frame = AddSection("FRAME", FrameColor, "FPS: --\nTIME: -- MS  RANGE: --");
            Rendering = AddSection("RENDERING", RenderingColor, "DRAW: --  PRIM: --  VERT: --\nUPLOAD: --  DATA: --");
            Simulation = AddSection("SIMULATION", SimulationColor, "SIM: --  FIXED: --  SCALE: --");
            Physics = AddSection("PHYSICS", PhysicsColor, "PHYS: --  CONTACT: --\nCHECKS: --");
            Display = AddSection(
                "DISPLAY",
                DisplayColor,
                "WINDOW: --  STATE: --  VSYNC: --\nFRAMEBUFFER: --  SCALE: --\nVIEWPORT: --  OFFSET: --");
            Gui = AddSection("GUI", GuiColor, "LOGICAL: --  SCALE: --  REF: --");
            Memory = AddSection("MEMORY", MemoryColor, "MEM: --  GC: --");
        }

        private GuiLabel AddSection(string name, Vector4D<float> color, string initialValue)
        {
            var section = _panel.Add(new GuiPanel
            {
                Padding = 0.0f,
                // Match the one-pixel visual gap between rows in the bitmap font.
                Spacing = TextScale,
                BackgroundColor = Transparent
            });
            var heading = section.AddLabel($"[{name}]");
            ConfigureLabel(heading, color);
            var value = section.AddLabel(initialValue);
            ConfigureLabel(value, new Vector4D<float>(0.92f, 0.95f, 1.0f, 1.0f));
            return value;
        }

        private static void ConfigureLabel(GuiLabel label, Vector4D<float> color)
        {
            label.Scale = TextScale;
            label.Padding = 0.0f;
            label.BackgroundColor = Transparent;
            label.Color = color;
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
