using Example_Game.Logic.scenes;
using Schreadt_Engine.Component;
using Schreadt_Engine.Component.Logic;
using Schreadt_Engine.Core;
using Silk.NET.Maths;

namespace Schreadt_Engine.Tests.Core;

public sealed class BackgroundTests
{
    [Fact]
    public void Scene_DefaultsToEngineGridAndAcceptsGameBackground()
    {
        var scene = new Scene("background-test", new EmptySceneLogic());

        Assert.IsType<GridBackground2D>(scene.Background);
        Assert.IsAssignableFrom<IBackground2D>(scene.Background);

        var custom = new RecordingBackground();
        scene.Background = custom;
        Assert.Same(custom, scene.Background);

        scene.Background = null;
        Assert.Null(scene.Background);
    }

    [Fact]
    public void Camera_CreatesParallaxViewAroundConfiguredOrigin()
    {
        var camera = new Camera
        {
            Position = new Vector2D<double>(10.0, 6.0),
            OrthographicSize = 2.0,
            RotationRadians = 0.35
        };
        var background = new RecordingBackground
        {
            ParallaxFactor = 0.25,
            ParallaxOrigin = new Vector2D<double>(2.0, -2.0)
        };

        var view = camera.CreateBackgroundView(2.0, background);

        Assert.Equal(new Vector2D<double>(4.0, 0.0), view.Position);
        Assert.Equal(2.0, view.OrthographicSize);
        Assert.Equal(2.0, view.AspectRatio);
        Assert.Equal(0.35, view.RotationRadians, 10);
    }

    [Fact]
    public void Camera_RejectsInvalidCustomBackgroundParallax()
    {
        var camera = new Camera();
        var background = new RecordingBackground { ParallaxFactor = -0.1 };
        Assert.Throws<ArgumentOutOfRangeException>(() => camera.CreateBackgroundView(1.0, background));

        background.ParallaxFactor = double.NaN;
        Assert.Throws<ArgumentOutOfRangeException>(() => camera.CreateBackgroundView(1.0, background));

        background.ParallaxFactor = 1.0;
        background.ParallaxOrigin = new Vector2D<double>(double.PositiveInfinity, 0.0);
        Assert.Throws<ArgumentOutOfRangeException>(() => camera.CreateBackgroundView(1.0, background));
    }

    [Fact]
    public void GridBackground_RendersMinorMajorAndAxisLineBatches()
    {
        var context = new RecordingBackgroundRenderContext(new BackgroundView2D(
            Vector2D<double>.Zero,
            0.0,
            1.0,
            1.0,
            new Vector2D<double>(-1.0, -1.0),
            new Vector2D<double>(1.0, 1.0)));
        var grid = new GridBackground2D
        {
            CellSize = 0.5,
            MajorLineEvery = 2
        };

        grid.Render(context);

        Assert.Equal(3, context.LineBatches.Count);
        Assert.Contains(context.LineBatches[2].Lines,
            line => line.Start.X == 0.0 && line.End.X == 0.0);
        Assert.Contains(context.LineBatches[2].Lines,
            line => line.Start.Y == 0.0 && line.End.Y == 0.0);
        Assert.NotEmpty(context.LineBatches[0].Lines);
        Assert.NotEmpty(context.LineBatches[1].Lines);
    }

    [Fact]
    public void GridBackground_ValidatesParallaxConfiguration()
    {
        var grid = new GridBackground2D();

        Assert.Throws<ArgumentOutOfRangeException>(() => grid.ParallaxFactor = -1.0);
        Assert.Throws<ArgumentOutOfRangeException>(() => grid.ParallaxFactor = double.NaN);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            grid.ParallaxOrigin = new Vector2D<double>(0.0, double.PositiveInfinity));
    }

    [Fact]
    public void ExampleLevelBackground_UsesBackgroundContractAndParallax()
    {
        IBackground2D background = new LevelBackground(1);
        var context = new RecordingBackgroundRenderContext(new BackgroundView2D(
            Vector2D<double>.Zero,
            0.0,
            2.4,
            16.0 / 9.0,
            new Vector2D<double>(-4.3, -2.4),
            new Vector2D<double>(4.3, 2.4)));

        background.Render(context);

        Assert.InRange(background.ParallaxFactor, 0.0, 0.999999);
        Assert.True(context.CircleCount > 0);
        Assert.True(context.RectangleCount > 0);
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

    private sealed class RecordingBackground : IBackground2D
    {
        public bool Enabled { get; set; } = true;
        public double ParallaxFactor { get; set; } = 1.0;
        public Vector2D<double> ParallaxOrigin { get; set; }
        public void Render(IBackgroundRenderContext2D context)
        {
        }
    }

    private sealed class RecordingBackgroundRenderContext(BackgroundView2D view) : IBackgroundRenderContext2D
    {
        public Vector2D<int> ViewportSize => new(1280, 720);
        public BackgroundView2D View { get; } = view;
        public List<(IReadOnlyList<LineSegment2D> Lines, Vector4D<float> Color)> LineBatches { get; } = [];
        public int CircleCount { get; private set; }
        public int RectangleCount { get; private set; }

        public void DrawLines(IReadOnlyList<LineSegment2D> lines, Vector4D<float> color) =>
            LineBatches.Add((lines.ToArray(), color));

        public void DrawCircle(Vector2D<double> center, double radius, Vector4D<float> color) => CircleCount++;

        public void DrawRectangle(
            Vector2D<double> center,
            Vector2D<double> size,
            Vector4D<float> color,
            double rotationRadians = 0.0) => RectangleCount++;

        public void DrawPolygon(
            Vector2D<double> center,
            IReadOnlyList<Vector2D<double>> localVertices,
            Vector2D<double> scale,
            double rotationRadians,
            Vector4D<float> color)
        {
        }

        public void DrawSprite(
            string imageAssetId,
            Vector2D<double> center,
            Vector2D<double> size,
            Vector4D<float> tint,
            double rotationRadians = 0.0,
            TextureRegion? region = null,
            TextureSampling sampling = TextureSampling.Linear)
        {
        }

        public void DrawText(
            string text,
            Vector2D<float> position,
            float scale,
            Vector4D<float> color,
            Vector4D<float> backgroundColor,
            float padding = 0.0f)
        {
        }

        public void DrawScreenRectangle(
            Vector2D<float> position,
            Vector2D<float> size,
            Vector4D<float> color)
        {
        }
    }
}
