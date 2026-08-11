using Schreadt_Engine.Component;
using Schreadt_Engine.Component.Logic;
using Schreadt_Engine.Core;
using Schreadt_Engine.Gui;
using Silk.NET.Maths;

namespace Schreadt_Engine.Tests.Gui;

public sealed class SceneGuiAndScreenTests
{
    [Fact]
    public void SceneTransition_DetachesAndClearsPreviousSceneGui()
    {
        var gui = new GuiSystem();
        var runtime = new RuntimeController();
        var scenes = new SceneManager(gui, runtime);
        GuiLayer? firstLayer = null;
        GuiScreen? firstScreen = null;
        GuiButton? firstButton = null;
        scenes.RegisterScene("first", () => new CallbackSceneLogic(scene =>
        {
            firstLayer = scene.Gui;
            firstButton = scene.Gui.AddButton("FIRST");
            firstScreen = scene.Screens.Push(new GuiScreen("overlay", new GuiPanel()));
        }));
        scenes.RegisterScene("second", () => new CallbackSceneLogic(scene => scene.Gui.AddButton("SECOND")));
        scenes.LoadScene("first");
        scenes.Init();

        Assert.True(firstLayer!.Attached);
        Assert.Single(gui.Layers);
        Assert.Single(firstLayer.Elements);
        Assert.True(firstScreen!.IsOpen);

        scenes.LoadScene("second");
        scenes.ProcessPendingSceneChange();

        Assert.False(firstLayer.Attached);
        Assert.Empty(firstLayer.Elements);
        Assert.Empty(firstLayer.Screens.Screens);
        Assert.False(firstScreen.IsOpen);
        Assert.Single(gui.Layers);
        Assert.Same(scenes.CurrentScene!.Gui, gui.Layers[0]);
        Assert.Same(firstButton, gui.Add(firstButton!));
    }

    [Fact]
    public void PausingScreen_ResumesOnlyWhenStackOwnedThePause()
    {
        var runtime = new RuntimeController();
        var layer = new GuiLayer();
        layer.Screens.SetRuntime(runtime);
        var screen = new GuiScreen("pause", new GuiPanel()) { PausesSimulation = true };

        layer.Screens.Push(screen);
        Assert.True(runtime.IsPaused);
        layer.Screens.Pop();
        Assert.False(runtime.IsPaused);

        runtime.Pause();
        layer.Screens.Push(screen);
        layer.Screens.Pop();
        Assert.True(runtime.IsPaused);
    }

    [Fact]
    public void MultiplePausingScreenStacks_HoldIndependentPauseRequests()
    {
        var runtime = new RuntimeController();
        var first = new GuiLayer();
        var second = new GuiLayer();
        first.Screens.SetRuntime(runtime);
        second.Screens.SetRuntime(runtime);
        first.Screens.Push(new GuiScreen("first", new GuiPanel()) { PausesSimulation = true });
        second.Screens.Push(new GuiScreen("second", new GuiPanel()) { PausesSimulation = true });

        first.Screens.Clear();
        Assert.True(runtime.IsPaused);

        second.Screens.Clear();
        Assert.False(runtime.IsPaused);
    }

    [Fact]
    public void ModalScreen_BlocksControlsBelowIt()
    {
        using var input = new InputManager();
        var gui = new GuiSystem();
        var layer = gui.AddLayer(new GuiLayer());
        var button = layer.AddButton("UNDERLAY");
        var clicks = 0;
        button.Clicked += (_, _) => clicks++;
        layer.Screens.Push(new GuiScreen("modal", new GuiPanel
        {
            Position = new Vector2D<float>(500.0f, 500.0f)
        }));
        gui.Render(new TestRenderContext());

        Click(input, gui, new System.Numerics.Vector2(2.0f, 2.0f));

        Assert.Equal(0, clicks);
        Assert.False(button.IsHovered);
    }

    [Fact]
    public void EscapeDismissesTopScreenAndResumesSimulation()
    {
        using var input = new InputManager();
        var runtime = new RuntimeController();
        var gui = new GuiSystem();
        var layer = gui.AddLayer(new GuiLayer());
        layer.Screens.SetRuntime(runtime);
        var screen = new GuiScreen("pause", new GuiPanel())
        {
            PausesSimulation = true,
            DismissOnEscape = true
        };
        layer.Screens.Push(screen);

        input.ProcessKeyDown(InputKey.Escape);
        gui.Update(input);

        Assert.False(screen.IsOpen);
        Assert.Empty(layer.Screens.Screens);
        Assert.False(runtime.IsPaused);
    }

    [Fact]
    public void RemovingLayer_ReleasesPointerCapture()
    {
        using var input = new InputManager();
        var gui = new GuiSystem();
        var layer = gui.AddLayer(new GuiLayer());
        var button = layer.AddButton("CAPTURE");
        gui.Render(new TestRenderContext());
        input.ProcessMouseMove(new System.Numerics.Vector2(2.0f, 2.0f));
        input.ProcessMouseDown(InputMouseButton.Left);
        gui.Update(input);
        Assert.True(button.IsPressed);
        Assert.True(gui.IsPointerCaptured);

        gui.RemoveLayer(layer);

        Assert.False(button.IsPressed);
        Assert.False(button.IsHovered);
        Assert.False(gui.IsPointerCaptured);
    }

    [Fact]
    public void PendingSceneChange_CanBeProcessedWhileFlowScreenIsPaused()
    {
        var gui = new GuiSystem();
        var runtime = new RuntimeController();
        var scenes = new SceneManager(gui, runtime);
        scenes.RegisterScene("paused", () => new CallbackSceneLogic(scene =>
            scene.Screens.Push(new GuiScreen("game-over", new GuiPanel()) { PausesSimulation = true })));
        scenes.RegisterScene("next", () => new CallbackSceneLogic(_ => { }));
        scenes.LoadScene("paused");
        scenes.Init();
        Assert.True(runtime.IsPaused);

        scenes.LoadScene("next");
        scenes.ProcessPendingSceneChange();

        Assert.Equal("next", scenes.CurrentSceneName);
        Assert.False(runtime.IsPaused);
        Assert.Single(gui.Layers);
    }

    [Fact]
    public void ScreenStack_RejectsDuplicateNamesAndReportsLifecycle()
    {
        var stack = new GuiLayer().Screens;
        var opened = 0;
        var closed = 0;
        var first = new GuiScreen("menu", new GuiPanel());
        first.Opened += _ => opened++;
        first.Closed += _ => closed++;

        stack.Push(first);
        Assert.Throws<InvalidOperationException>(() => stack.Push(new GuiScreen("menu", new GuiPanel())));
        Assert.True(stack.Remove("menu"));

        Assert.Equal(1, opened);
        Assert.Equal(1, closed);
        Assert.False(first.IsOpen);
    }

    private static void Click(InputManager input, GuiSystem gui, System.Numerics.Vector2 position)
    {
        input.ProcessMouseMove(position);
        input.ProcessMouseDown(InputMouseButton.Left);
        input.ProcessMouseUp(InputMouseButton.Left);
        gui.Update(input);
    }

    private sealed class CallbackSceneLogic(Action<Scene> initialize) : SceneLogic
    {
        public override void Init() => initialize(Scene);

        public override void Update(double dt)
        {
        }
    }

    private sealed class TestRenderContext : IRenderContext2D
    {
        public Vector2D<int> ViewportSize => new(800, 600);

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
            Vector4D<float> color)
        {
        }
    }
}
