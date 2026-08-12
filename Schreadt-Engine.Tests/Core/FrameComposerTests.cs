using Schreadt_Engine.Collision;
using Schreadt_Engine.Component;
using Schreadt_Engine.Component.Logic;
using Schreadt_Engine.Component.PreFab;
using Schreadt_Engine.Core;
using Schreadt_Engine.Gui;
using Silk.NET.Maths;
using System.Reflection;

namespace Schreadt_Engine.Tests.Core;

public sealed class FrameComposerTests
{
    private static readonly Vector4D<float> FarBackgroundColor = new(0.9f, 0.1f, 0.1f, 1.0f);
    private static readonly Vector4D<float> NearBackgroundColor = new(0.1f, 0.9f, 0.1f, 1.0f);
    private static readonly Vector4D<float> SceneColor = new(0.1f, 0.2f, 0.9f, 1.0f);
    private static readonly Vector4D<float> DiagnosticColor = new(0.95f, 0.8f, 0.1f, 0.8f);
    private static readonly Vector4D<float> GuiColor = new(0.8f, 0.1f, 0.8f, 1.0f);

    [Fact]
    public void ComposeFrame_RecordsBackgroundSceneDiagnosticsAndGuiInOrder()
    {
        var scene = new Scene("composition", new EmptySceneLogic())
        {
            Background = new LayeredBackground2D
            {
                new DrawingBackground(0.25, FarBackgroundColor, drawCircle: false),
                new DrawingBackground(0.5, NearBackgroundColor, drawCircle: true)
            }
        };
        var sceneRectangle = new Rectangle2D
        {
            Position = new Vector2D<double>(3.0, 1.0),
            Size = new Vector2D<double>(1.0, 1.0),
            Color = SceneColor
        };
        sceneRectangle.AddComponent(new AxisAlignedBoxCollider2D(Vector2D<double>.One));
        scene.AddChild(sceneRectangle);
        scene.Collisions.DebugDraw.Enabled = true;
        scene.Collisions.DebugDraw.StaticColor = DiagnosticColor;
        scene.Init();

        var gui = new GuiSystem(400.0f);
        var panel = gui.AddPanel();
        panel.BackgroundColor = GuiColor;
        panel.AddLabel("FRAME");

        var camera = new Camera
        {
            Position = new Vector2D<double>(8.0, 2.0),
            OrthographicSize = 3.0,
            RotationRadians = 0.2
        };
        var renderer = new RecordingFrameRenderer();

        new FrameComposer2D().ComposeFrame(renderer, camera, scene, gui);

        Assert.Equal("begin", renderer.Commands[0].Kind);
        Assert.Equal("end", renderer.Commands[^1].Kind);
        Assert.Equal(camera.RenderPosition, renderer.Commands[0].View.Center);
        Assert.Equal(2.0, renderer.Commands[0].View.AspectRatio);

        var far = renderer.Find("rectangle", FarBackgroundColor);
        var near = renderer.Find("circle", NearBackgroundColor);
        var sceneDraw = renderer.Find("rectangle", SceneColor);
        var diagnostics = renderer.Find("rectangle", DiagnosticColor);
        var guiDraw = renderer.Find("screen-rectangle", GuiColor);

        Assert.True(far.Index < near.Index);
        Assert.True(near.Index < sceneDraw.Index);
        Assert.True(sceneDraw.Index < diagnostics.Index);
        Assert.True(diagnostics.Index < guiDraw.Index);
        Assert.Equal(new Vector2D<double>(2.0, 0.5), far.Command.View.Center);
        Assert.Equal(new Vector2D<double>(4.0, 1.0), near.Command.View.Center);
        Assert.Equal(camera.RenderPosition, sceneDraw.Command.View.Center);
        Assert.Equal(camera.RenderPosition, diagnostics.Command.View.Center);
    }

    [Fact]
    public void ComposeFrame_PreservesPixelRenderingThroughComposer()
    {
        using var surface = new PixelSurface(2, 2);
        var scene = new Scene("pixels", new EmptySceneLogic()) { Background = null };
        scene.AddChild(new PixelGameObject(surface));
        scene.Init();
        var renderer = new RecordingFrameRenderer();

        new FrameComposer2D().ComposeFrame(renderer, new Camera(), scene);

        Assert.Contains(renderer.Commands, command => command.Kind == "pixels");
        Assert.Same(surface, renderer.LastPixelSurface);
    }

    [Fact]
    public void ComposeFrame_EndsFailedFrameAndCanBeReused()
    {
        var composer = new FrameComposer2D();
        var renderer = new RecordingFrameRenderer();
        var failure = new InvalidOperationException("scene draw failed");
        var failingScene = new Scene("failing", new EmptySceneLogic()) { Background = null };
        failingScene.AddChild(new ThrowingGameObject(failure));
        failingScene.Init();

        var thrown = Assert.Throws<InvalidOperationException>(() =>
            composer.ComposeFrame(renderer, new Camera(), failingScene));

        Assert.Same(failure, thrown);
        Assert.Equal(1, renderer.Commands.Count(command => command.Kind == "begin"));
        Assert.Equal(1, renderer.Commands.Count(command => command.Kind == "end"));

        var nextScene = new Scene("next", new EmptySceneLogic()) { Background = null };
        nextScene.Init();
        composer.ComposeFrame(renderer, new Camera(), nextScene);

        Assert.Equal(2, renderer.Commands.Count(command => command.Kind == "begin"));
        Assert.Equal(2, renderer.Commands.Count(command => command.Kind == "end"));
    }

    [Fact]
    public void CameraView_IsImmutableValidatedAndProjectsWithoutCameraObject()
    {
        var view = new CameraView2D(
            new Vector2D<double>(3.0, -2.0),
            orthographicSize: 2.0,
            aspectRatio: 2.0,
            rotationRadians: Math.PI / 2.0);

        var projected = view.WorldToNormalizedDevicePoint(new Vector2D<double>(3.0, 0.0));
        var restored = view.NormalizedDeviceToWorldPoint(projected);

        Assert.Equal(new Vector2D<double>(0.5, 0.0), projected, new Vector2DComparer(10));
        Assert.Equal(new Vector2D<double>(3.0, 0.0), restored, new Vector2DComparer(10));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CameraView2D(Vector2D<double>.Zero, 0.0, 1.0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CameraView2D(Vector2D<double>.Zero, 1.0, double.NaN));
    }

    [Fact]
    public void Renderer_PublicDrawingBoundaryDoesNotAcceptDomainObjects()
    {
        var parameterTypes = typeof(Renderer)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .SelectMany(method => method.GetParameters())
            .Select(parameter => parameter.ParameterType)
            .ToArray();

        Assert.DoesNotContain(parameterTypes, type => typeof(GameObject).IsAssignableFrom(type));
        Assert.DoesNotContain(parameterTypes, type => type == typeof(Camera));
        Assert.DoesNotContain(parameterTypes, type => type == typeof(Scene));
        Assert.DoesNotContain(parameterTypes, type => type == typeof(GuiSystem));
        Assert.DoesNotContain(parameterTypes, type => typeof(IBackground2D).IsAssignableFrom(type));
        Assert.Contains(typeof(IFrameRenderer2D), typeof(Renderer).GetInterfaces());
        Assert.DoesNotContain(typeof(IBackgroundRenderContext2D), typeof(Renderer).GetInterfaces());
    }

    private sealed class DrawingBackground(
        double parallaxFactor,
        Vector4D<float> color,
        bool drawCircle) : IBackground2D
    {
        public bool Enabled => true;
        public double ParallaxFactor { get; } = parallaxFactor;
        public Vector2D<double> ParallaxOrigin => Vector2D<double>.Zero;

        public void Render(IBackgroundRenderContext2D context)
        {
            if (drawCircle) context.DrawCircle(Vector2D<double>.Zero, 1.0, color);
            else context.DrawRectangle(Vector2D<double>.Zero, Vector2D<double>.One, color);
        }
    }

    private sealed class PixelGameObject(PixelSurface surface) : GameObject
    {
        protected override void OnRender(IRenderContext2D renderer)
        {
            var pixels = Assert.IsAssignableFrom<IPixelRenderContext2D>(renderer);
            pixels.DrawScreenPixels(surface, TextureSampling.Nearest);
        }
    }

    private sealed class ThrowingGameObject(Exception exception) : GameObject
    {
        protected override void OnRender(IRenderContext2D renderer) => throw exception;
    }

    private sealed class EmptySceneLogic : SceneLogic
    {
        public override void Init()
        {
        }

        public override void Update(double dt)
        {
        }
    }

    private sealed class RecordingFrameRenderer : IFrameRenderer2D
    {
        private CameraView2D _view;
        private bool _frameActive;

        public Vector2D<int> ViewportOffset => Vector2D<int>.Zero;
        public Vector2D<int> ViewportSize => new(800, 400);
        public RenderStatistics Statistics { get; private set; }
        public List<RecordedCommand> Commands { get; } = [];
        public PixelSurface? LastPixelSurface { get; private set; }

        public void BeginFrame(CameraView2D view)
        {
            Assert.False(_frameActive);
            _frameActive = true;
            _view = view;
            Commands.Add(new RecordedCommand("begin", default, view));
        }

        public void SetView(CameraView2D view)
        {
            Assert.True(_frameActive);
            _view = view;
            Commands.Add(new RecordedCommand("view", default, view));
        }

        public void EndFrame()
        {
            Assert.True(_frameActive);
            Commands.Add(new RecordedCommand("end", default, _view));
            _frameActive = false;
            Statistics = new RenderStatistics(Commands.Count, Commands.Count, Commands.Count);
        }

        public void Resize(int width, int height)
        {
        }

        public void DrawCircle(Vector2D<double> center, double radius, Vector4D<float> color) =>
            Record("circle", color);

        public void DrawRectangle(
            Vector2D<double> center,
            Vector2D<double> size,
            Vector4D<float> color,
            double rotationRadians = 0.0) => Record("rectangle", color);

        public void DrawPolygon(
            Vector2D<double> center,
            IReadOnlyList<Vector2D<double>> localVertices,
            Vector2D<double> scale,
            double rotationRadians,
            Vector4D<float> color) => Record("polygon", color);

        public void DrawSprite(
            string imageAssetId,
            Vector2D<double> center,
            Vector2D<double> size,
            Vector4D<float> tint,
            double rotationRadians = 0.0,
            TextureRegion? region = null,
            TextureSampling sampling = TextureSampling.Linear) => Record("sprite", tint);

        public void DrawText(
            string text,
            Vector2D<float> position,
            float scale,
            Vector4D<float> color,
            Vector4D<float> backgroundColor,
            float padding = 0.0f) => Record("text", color);

        public void DrawScreenRectangle(
            Vector2D<float> position,
            Vector2D<float> size,
            Vector4D<float> color) => Record("screen-rectangle", color);

        public void DrawScreenPixels(
            PixelSurface surface,
            TextureSampling sampling = TextureSampling.Nearest)
        {
            LastPixelSurface = surface;
            Record("pixels", default);
        }

        public void DrawLines(IReadOnlyList<LineSegment2D> lines, Vector4D<float> color) =>
            Record("lines", color);

        public void Dispose()
        {
        }

        public (int Index, RecordedCommand Command) Find(string kind, Vector4D<float> color)
        {
            var index = Commands.FindIndex(command => command.Kind == kind && command.Color == color);
            Assert.True(index >= 0, $"Could not find {kind} with color {color}.");
            return (index, Commands[index]);
        }

        private void Record(string kind, Vector4D<float> color)
        {
            Assert.True(_frameActive);
            Commands.Add(new RecordedCommand(kind, color, _view));
        }
    }

    private readonly record struct RecordedCommand(
        string Kind,
        Vector4D<float> Color,
        CameraView2D View);

    private sealed class Vector2DComparer(int precision) : IEqualityComparer<Vector2D<double>>
    {
        public bool Equals(Vector2D<double> left, Vector2D<double> right) =>
            Math.Round(left.X - right.X, precision) == 0.0 &&
            Math.Round(left.Y - right.Y, precision) == 0.0;

        public int GetHashCode(Vector2D<double> value) => 0;
    }
}
