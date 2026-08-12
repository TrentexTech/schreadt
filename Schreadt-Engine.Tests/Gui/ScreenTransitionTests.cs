using Schreadt_Engine.Animation.Tweening;
using Schreadt_Engine.Core;
using Schreadt_Engine.Gui;
using Silk.NET.Maths;

namespace Schreadt_Engine.Tests.Gui;

public sealed class ScreenTransitionTests
{
    [Fact]
    public void CrossFade_BlendsBothScreensAndCompletesPush()
    {
        using var input = new InputManager();
        var gui = new GuiSystem(600.0f);
        var layer = gui.AddLayer(new GuiLayer());
        var outgoing = CreateScreen("outgoing", new Vector4D<float>(1.0f, 0.0f, 0.0f, 1.0f));
        var incoming = CreateScreen("incoming", new Vector4D<float>(0.0f, 1.0f, 0.0f, 1.0f));
        layer.Screens.Push(outgoing);
        layer.Screens.Push(incoming, new CrossFadeScreenTransition(1.0));
        var renderer = new RecordingRenderContext();

        gui.Update(input, 0.5);
        gui.Render(renderer);

        Assert.True(layer.Screens.IsTransitioning);
        Assert.Collection(
            renderer.ScreenRectangles,
            draw => Assert.Equal(0.5f, draw.Color.W, 5),
            draw => Assert.Equal(0.5f, draw.Color.W, 5));

        gui.Update(input, 0.5);

        Assert.False(layer.Screens.IsTransitioning);
        Assert.True(outgoing.IsOpen);
        Assert.True(incoming.IsOpen);
        Assert.Same(incoming, layer.Screens.Top);
    }

    [Fact]
    public void FadeToColor_HidesScreenSwitchAtOpaqueMidpoint()
    {
        using var input = new InputManager();
        var gui = new GuiSystem(600.0f);
        var layer = gui.AddLayer(new GuiLayer());
        var outgoing = CreateScreen("outgoing", new Vector4D<float>(1.0f, 0.0f, 0.0f, 1.0f));
        var incoming = CreateScreen("incoming", new Vector4D<float>(0.0f, 1.0f, 0.0f, 1.0f));
        var fadeColor = new Vector4D<float>(0.0f, 0.0f, 1.0f, 1.0f);
        layer.Screens.Push(outgoing);
        layer.Screens.Push(incoming, new FadeToColorScreenTransition(fadeColor, 1.0));
        var renderer = new RecordingRenderContext();

        gui.Update(input, 0.25);
        gui.Render(renderer);

        Assert.Collection(
            renderer.ScreenRectangles,
            draw => Assert.Equal(outgoing.Root.Bounds.Position, draw.Position),
            draw =>
            {
                Assert.Equal(fadeColor.X, draw.Color.X);
                Assert.Equal(0.5f, draw.Color.W, 5);
            });

        renderer.ScreenRectangles.Clear();
        gui.Update(input, 0.25);
        gui.Render(renderer);

        Assert.Collection(
            renderer.ScreenRectangles,
            draw => Assert.Equal(incoming.Root.Bounds.Position, draw.Position),
            draw => Assert.Equal(fadeColor, draw.Color));
    }

    [Fact]
    public void Slide_UsesLogicalViewportAndRestoresInteractiveBounds()
    {
        using var input = new InputManager();
        var gui = new GuiSystem(600.0f);
        var layer = gui.AddLayer(new GuiLayer());
        var outgoing = CreateScreen("outgoing", new Vector4D<float>(1.0f, 0.0f, 0.0f, 1.0f));
        var incoming = CreateScreen("incoming", new Vector4D<float>(0.0f, 1.0f, 0.0f, 1.0f));
        layer.Screens.Push(outgoing);
        layer.Screens.Push(incoming, new SlideScreenTransition(GuiSlideDirection.Right, 1.0));
        var renderer = new RecordingRenderContext();

        gui.Update(input, 0.5);
        gui.Render(renderer);

        Assert.Collection(
            renderer.ScreenRectangles,
            draw => Assert.Equal(-400.0f, draw.Position.X, 4),
            draw => Assert.Equal(400.0f, draw.Position.X, 4));
        Assert.Equal(Vector2D<float>.Zero, outgoing.Root.Bounds.Position);
        Assert.Equal(Vector2D<float>.Zero, incoming.Root.Bounds.Position);
    }

    [Fact]
    public void Pop_AdvancesWithUnscaledTimeWhileScreenPausesSimulation()
    {
        using var input = new InputManager();
        var runtime = new RuntimeController();
        var gui = new GuiSystem();
        var layer = gui.AddLayer(new GuiLayer());
        layer.Screens.SetRuntime(runtime);
        var screen = CreateScreen("pause", Vector4D<float>.One);
        screen.PausesSimulation = true;
        layer.Screens.Push(screen);

        var popped = layer.Screens.Pop(new CrossFadeScreenTransition(0.25));
        var pausedFrame = runtime.Advance(0.5);
        gui.Update(input, runtime.UnscaledDeltaTime);

        Assert.Same(screen, popped);
        Assert.False(pausedFrame.ShouldUpdateSimulation);
        Assert.False(layer.Screens.IsTransitioning);
        Assert.False(screen.IsOpen);
        Assert.False(runtime.IsPaused);
    }

    [Fact]
    public void Transition_BlocksScreenInputUntilItCompletes()
    {
        using var input = new InputManager();
        var gui = new GuiSystem(600.0f);
        var layer = gui.AddLayer(new GuiLayer());
        var button = new GuiButton("INCOMING");
        var clicks = 0;
        button.Clicked += (_, _) => clicks++;
        layer.Screens.Push(new GuiScreen("incoming", button), new CrossFadeScreenTransition(1.0));
        gui.Update(input, 0.5);
        gui.Render(new RecordingRenderContext());

        Click(input, gui, new System.Numerics.Vector2(2.0f, 2.0f), 0.5);
        Assert.Equal(0, clicks);
        Assert.False(layer.Screens.IsTransitioning);

        gui.Render(new RecordingRenderContext());
        Click(input, gui, new System.Numerics.Vector2(2.0f, 2.0f), 0.0);
        Assert.Equal(1, clicks);
    }

    [Fact]
    public void ReplaceTop_ClosesOutgoingScreenOnlyAfterTransition()
    {
        using var input = new InputManager();
        var gui = new GuiSystem();
        var layer = gui.AddLayer(new GuiLayer());
        var outgoing = CreateScreen("outgoing", Vector4D<float>.One);
        var incoming = CreateScreen("incoming", Vector4D<float>.One);
        layer.Screens.Push(outgoing);

        layer.Screens.ReplaceTop(incoming, new CrossFadeScreenTransition(0.5));

        Assert.True(outgoing.IsOpen);
        Assert.True(incoming.IsOpen);
        Assert.Throws<InvalidOperationException>(() => layer.Screens.Pop());

        gui.Update(input, 0.5);

        Assert.False(outgoing.IsOpen);
        Assert.True(incoming.IsOpen);
        Assert.Single(layer.Screens.Screens);
        Assert.Same(incoming, layer.Screens.Top);
    }

    [Fact]
    public void ConfiguredTransitions_AreUsedForPushAndEscapeDismissal()
    {
        using var input = new InputManager();
        var gui = new GuiSystem();
        var layer = gui.AddLayer(new GuiLayer());
        var transition = new CrossFadeScreenTransition(0.25);
        var screen = CreateScreen("dismissable", Vector4D<float>.One);
        screen.DismissOnEscape = true;
        screen.OpeningTransition = transition;
        screen.ClosingTransition = transition;

        layer.Screens.Push(screen);
        Assert.True(layer.Screens.IsTransitioning);
        gui.Update(input, 0.25);

        input.ProcessKeyDown(InputKey.Escape);
        gui.Update(input, 0.0);

        Assert.True(layer.Screens.IsTransitioning);
        Assert.True(screen.IsOpen);

        gui.Update(input, 0.25);
        Assert.False(screen.IsOpen);
        Assert.Empty(layer.Screens.Screens);
    }

    [Fact]
    public void Clear_CancelsTransitionAndReleasesAllScreens()
    {
        var layer = new GuiLayer();
        var first = CreateScreen("first", Vector4D<float>.One);
        var second = CreateScreen("second", Vector4D<float>.One);
        layer.Screens.Push(first);
        layer.Screens.Push(second, new CrossFadeScreenTransition());

        layer.Screens.Clear();

        Assert.False(layer.Screens.IsTransitioning);
        Assert.Empty(layer.Screens.Screens);
        Assert.False(first.IsOpen);
        Assert.False(second.IsOpen);
    }

    [Fact]
    public void TransitionDefinitions_RejectInvalidConfigurationAndEasingOutput()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CrossFadeScreenTransition(0.0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new FadeToColorScreenTransition(new Vector4D<float>(float.NaN, 0.0f, 0.0f, 1.0f)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SlideScreenTransition((GuiSlideDirection)99));

        var transition = new CrossFadeScreenTransition(1.0, _ => double.NaN);
        Assert.Throws<InvalidOperationException>(() =>
            transition.CreateFrame(0.5, true, new Vector2D<float>(800.0f, 600.0f)));
    }

    [Fact]
    public void CustomEasing_IsAppliedToTransitionProgress()
    {
        var transition = new CrossFadeScreenTransition(1.0, TweenEasings.QuadraticIn);

        var frame = transition.CreateFrame(0.5, true, new Vector2D<float>(800.0f, 600.0f));

        Assert.Equal(0.75f, frame.OutgoingOpacity, 5);
        Assert.Equal(0.25f, frame.IncomingOpacity, 5);
    }

    private static GuiScreen CreateScreen(string name, Vector4D<float> color) =>
        new(name, new GuiPanel
        {
            BackgroundColor = color,
            Position = Vector2D<float>.Zero
        });

    private static void Click(
        InputManager input,
        GuiSystem gui,
        System.Numerics.Vector2 position,
        double unscaledDeltaTime)
    {
        input.ProcessMouseMove(position);
        input.ProcessMouseDown(InputMouseButton.Left);
        input.ProcessMouseUp(InputMouseButton.Left);
        gui.Update(input, unscaledDeltaTime);
        input.EndFrame();
    }

    private sealed class RecordingRenderContext : IRenderContext2D
    {
        public Vector2D<int> ViewportSize => new(800, 600);

        internal List<ScreenRectangleDraw> ScreenRectangles { get; } = [];

        public void DrawCircle(Vector2D<double> center, double radius, Vector4D<float> color)
        {
        }

        public void DrawRectangle(
            Vector2D<double> center,
            Vector2D<double> size,
            Vector4D<float> color,
            double rotationRadians = 0.0)
        {
        }

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
            Vector4D<float> color) =>
            ScreenRectangles.Add(new ScreenRectangleDraw(position, size, color));
    }

    private readonly record struct ScreenRectangleDraw(
        Vector2D<float> Position,
        Vector2D<float> Size,
        Vector4D<float> Color);
}
