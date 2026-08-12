using Schreadt_Engine.Component;
using Schreadt_Engine.Gui;
using Silk.NET.Maths;
using System.Diagnostics;

namespace Schreadt_Engine.Core;

/// <summary>
/// Composes a scene frame from backgrounds, scene objects, collision
/// diagnostics, and GUI without depending on a particular drawing backend.
/// </summary>
public sealed class FrameComposer2D :
    IBackgroundRenderContext2D,
    IFrameCompositionContext2D
{
    private readonly HashSet<IBackground2D> _activeBackgrounds = new(ReferenceEqualityComparer.Instance);
    private IFrameRenderer2D? _renderer;
    private Camera? _camera;
    private CameraView2D _currentView;
    private double _aspectRatio;

    public Vector2D<int> ViewportSize => Renderer.ViewportSize;

    CameraView2D IFrameCompositionContext2D.View
    {
        get
        {
            EnsureComposing();
            return _currentView;
        }
    }

    public FrameCompositionStatistics Statistics { get; private set; } = FrameCompositionStatistics.Empty;

    public BackgroundView2D View
    {
        get
        {
            EnsureComposing();
            var (minimum, maximum) = _currentView.GetVisibleBounds();
            return new BackgroundView2D(
                _currentView.Center,
                _currentView.RotationRadians,
                _currentView.OrthographicSize,
                _currentView.AspectRatio,
                minimum,
                maximum);
        }
    }

    public void ComposeFrame(
        IFrameRenderer2D renderer,
        Camera camera,
        Scene scene,
        GuiSystem? gui = null)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(scene);
        if (_renderer is not null)
            throw new InvalidOperationException("The frame composer is already composing a frame.");
        var viewportSize = renderer.ViewportSize;
        if (viewportSize.X <= 0 || viewportSize.Y <= 0)
            throw new InvalidOperationException("The frame renderer viewport must be positive before composing a frame.");

        var aspectRatio = (double)viewportSize.X / viewportSize.Y;
        var initialView = camera.CreateView(aspectRatio);
        _renderer = renderer;
        _camera = camera;
        _aspectRatio = aspectRatio;
        _currentView = initialView;
        var frameBegun = false;
        var totalStarted = Stopwatch.GetTimestamp();
        var backgroundMilliseconds = 0.0;
        var beforeSceneMilliseconds = 0.0;
        var sceneMilliseconds = 0.0;
        var afterSceneMilliseconds = 0.0;
        var diagnosticsMilliseconds = 0.0;
        var beforeGuiMilliseconds = 0.0;
        var guiMilliseconds = 0.0;
        var passTimings = new List<FrameCompositionPassTiming>();

        try
        {
            renderer.BeginFrame(_currentView);
            frameBegun = true;

            backgroundMilliseconds = Measure(() =>
            {
                if (scene.Background is { Enabled: true } background) RenderBackground(background);
            });
            beforeSceneMilliseconds = RenderPasses(
                scene,
                FrameCompositionStage.BeforeScene,
                passTimings);
            sceneMilliseconds = Measure(() => scene.Render(this));
            afterSceneMilliseconds = RenderPasses(
                scene,
                FrameCompositionStage.AfterScene,
                passTimings);
            diagnosticsMilliseconds = Measure(() => scene.Collisions.DrawDiagnostics(this));
            beforeGuiMilliseconds = RenderPasses(
                scene,
                FrameCompositionStage.BeforeGui,
                passTimings);
            guiMilliseconds = Measure(() => gui?.Render(this));
        }
        finally
        {
            try
            {
                if (frameBegun) renderer.EndFrame();
            }
            finally
            {
                Statistics = new FrameCompositionStatistics(
                    Stopwatch.GetElapsedTime(totalStarted).TotalMilliseconds,
                    backgroundMilliseconds,
                    beforeSceneMilliseconds,
                    sceneMilliseconds,
                    afterSceneMilliseconds,
                    diagnosticsMilliseconds,
                    beforeGuiMilliseconds,
                    guiMilliseconds,
                    passTimings);
                _activeBackgrounds.Clear();
                _renderer = null;
                _camera = null;
                _currentView = default;
                _aspectRatio = 0.0;
            }
        }
    }

    private double RenderPasses(
        Scene scene,
        FrameCompositionStage stage,
        List<FrameCompositionPassTiming> passTimings)
    {
        var started = Stopwatch.GetTimestamp();
        var passes = scene.CompositionPasses
            .Select((pass, index) => (Pass: pass, Index: index))
            .Where(entry => entry.Pass.Stage == stage && entry.Pass.Enabled)
            .OrderBy(entry => entry.Pass.Order)
            .ThenBy(entry => entry.Index)
            .Select(entry => entry.Pass)
            .ToArray();

        foreach (var pass in passes)
        {
            var passStarted = Stopwatch.GetTimestamp();
            try
            {
                pass.Render(this);
            }
            finally
            {
                passTimings.Add(new FrameCompositionPassTiming(
                    pass.Name,
                    stage,
                    Stopwatch.GetElapsedTime(passStarted).TotalMilliseconds));
            }
        }

        return Stopwatch.GetElapsedTime(started).TotalMilliseconds;
    }

    private static double Measure(Action action)
    {
        var started = Stopwatch.GetTimestamp();
        action();
        return Stopwatch.GetElapsedTime(started).TotalMilliseconds;
    }

    public void RenderBackground(IBackground2D background)
    {
        ArgumentNullException.ThrowIfNull(background);
        EnsureComposing();
        if (!background.Enabled) return;
        if (!_activeBackgrounds.Add(background))
            throw new InvalidOperationException("A cycle was detected while rendering layered backgrounds.");

        var previousView = _currentView;
        var viewChanged = false;
        try
        {
            var backgroundView = _camera!.CreateBackgroundView(_aspectRatio, background);
            Renderer.SetView(backgroundView);
            _currentView = backgroundView;
            viewChanged = true;
            background.Render(this);
        }
        finally
        {
            try
            {
                if (viewChanged) Renderer.SetView(previousView);
            }
            finally
            {
                _currentView = previousView;
                _activeBackgrounds.Remove(background);
            }
        }
    }

    public void DrawCircle(Vector2D<double> center, double radius, Vector4D<float> color) =>
        Renderer.DrawCircle(center, radius, color);

    public void DrawRectangle(
        Vector2D<double> center,
        Vector2D<double> size,
        Vector4D<float> color,
        double rotationRadians = 0.0) =>
        Renderer.DrawRectangle(center, size, color, rotationRadians);

    public void DrawPolygon(
        Vector2D<double> center,
        IReadOnlyList<Vector2D<double>> localVertices,
        Vector2D<double> scale,
        double rotationRadians,
        Vector4D<float> color) =>
        Renderer.DrawPolygon(center, localVertices, scale, rotationRadians, color);

    public void DrawSprite(
        string imageAssetId,
        Vector2D<double> center,
        Vector2D<double> size,
        Vector4D<float> tint,
        double rotationRadians = 0.0,
        TextureRegion? region = null,
        TextureSampling sampling = TextureSampling.Linear) =>
        Renderer.DrawSprite(imageAssetId, center, size, tint, rotationRadians, region, sampling);

    public void DrawText(
        string text,
        Vector2D<float> position,
        float scale,
        Vector4D<float> color,
        Vector4D<float> backgroundColor,
        float padding = 0.0f) =>
        Renderer.DrawText(text, position, scale, color, backgroundColor, padding);

    public void DrawScreenRectangle(
        Vector2D<float> position,
        Vector2D<float> size,
        Vector4D<float> color) =>
        Renderer.DrawScreenRectangle(position, size, color);

    public void DrawScreenPixels(PixelSurface surface, TextureSampling sampling = TextureSampling.Nearest) =>
        Renderer.DrawScreenPixels(surface, sampling);

    public void DrawLines(IReadOnlyList<LineSegment2D> lines, Vector4D<float> color) =>
        Renderer.DrawLines(lines, color);

    private IFrameRenderer2D Renderer
    {
        get
        {
            EnsureComposing();
            return _renderer!;
        }
    }

    private void EnsureComposing()
    {
        if (_renderer is null)
            throw new InvalidOperationException("Draw calls require an active composed frame.");
    }
}
