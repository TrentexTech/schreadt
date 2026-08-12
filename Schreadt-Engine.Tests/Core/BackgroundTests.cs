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
    public void LayeredBackground_ManagesAndRendersEnabledLayersInOrder()
    {
        var first = new RecordingBackground { ParallaxFactor = 0.1 };
        var disabled = new RecordingBackground { Enabled = false, ParallaxFactor = 0.2 };
        var last = new RecordingBackground { ParallaxFactor = 0.3 };
        var background = new LayeredBackground2D { first, last };
        background.Insert(1, disabled);
        var context = new RecordingBackgroundRenderContext(new BackgroundView2D(
            Vector2D<double>.Zero,
            0.0,
            1.0,
            1.0,
            new Vector2D<double>(-1.0, -1.0),
            new Vector2D<double>(1.0, 1.0)));

        background.Render(context);

        Assert.Equal(3, background.Count);
        Assert.Same(disabled, background[1]);
        Assert.Equal([first, last], context.RenderedBackgrounds);
        Assert.True(background.Remove(disabled));
        Assert.Equal([first, last], background.Layers);
        background.Clear();
        Assert.Empty(background.Layers);
    }

    [Fact]
    public void LayeredBackground_RejectsDuplicatesAndHierarchyCycles()
    {
        var outer = new LayeredBackground2D();
        var inner = new LayeredBackground2D();
        var layer = new RecordingBackground();

        outer.Add(layer);
        Assert.Throws<InvalidOperationException>(() => outer.Add(layer));
        Assert.Throws<InvalidOperationException>(() => outer.Add(outer));

        outer.Add(inner);
        Assert.Throws<InvalidOperationException>(() => inner.Add(outer));
    }

    [Fact]
    public void ExampleLevelBackground_UsesMultipleParallaxLayers()
    {
        var background = LevelBackground.Create(1);
        var context = new RecordingBackgroundRenderContext(new BackgroundView2D(
            Vector2D<double>.Zero,
            0.0,
            2.4,
            16.0 / 9.0,
            new Vector2D<double>(-4.3, -2.4),
            new Vector2D<double>(4.3, 2.4)));

        background.Render(context);

        Assert.Equal(5, background.Count);
        Assert.Equal([0.0, 0.06, 0.16, 0.34, 0.52],
            background.Layers.Select(layer => layer.ParallaxFactor));
        var camera = new Camera { Position = new Vector2D<double>(10.0, 0.0) };
        var layerCenters = background.Layers
            .Select(layer => camera.CreateBackgroundView(1.0, layer).Position.X)
            .ToArray();
        var expectedCenters = new[] { 0.0, 0.6, 1.6, 3.4, 5.2 };
        for (var index = 0; index < expectedCenters.Length; index++)
            Assert.Equal(expectedCenters[index], layerCenters[index], 10);
        Assert.Equal(background.Layers, context.RenderedBackgrounds);
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
        public List<IBackground2D> RenderedBackgrounds { get; } = [];
        public int CircleCount { get; private set; }
        public int RectangleCount { get; private set; }

        public void RenderBackground(IBackground2D background)
        {
            if (!background.Enabled) return;
            RenderedBackgrounds.Add(background);
            background.Render(this);
        }

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
