using Schreadt_Engine.Component;
using Schreadt_Engine.Component.Logic;
using Schreadt_Engine.Core;
using Schreadt_Engine.Gui;
using Silk.NET.Maths;

namespace Schreadt_Engine.Tests.Core;

[Collection("Engine lifecycle")]
public sealed class SceneTransitionTests
{
    [Fact]
    public void FadeToColor_SwapsAtOpaqueMidpointAndUsesUnscaledTime()
    {
        var gui = new GuiSystem(600.0f);
        var runtime = new RuntimeController();
        var scenes = CreateInitializedManager(gui, runtime);
        var transition = new FadeToColorSceneTransition(
            new Vector4D<float>(0.1f, 0.2f, 0.3f, 1.0f),
            1.0);
        scenes.LoadScene("second", transition);

        scenes.ProcessPendingSceneChange(0.25);

        Assert.True(scenes.IsTransitioning);
        Assert.Same(transition, scenes.ActiveTransition);
        Assert.Equal("second", scenes.TransitionTargetSceneName);
        Assert.Equal(0.0, scenes.TransitionProgress);
        Assert.Equal("first", scenes.CurrentSceneName);
        Assert.True(runtime.IsPaused);
        Assert.True(gui.IsInputBlocked);
        Assert.False(runtime.Advance(0.25).ShouldUpdateSimulation);

        scenes.ProcessPendingSceneChange(runtime.UnscaledDeltaTime);
        Assert.Equal(0.25, scenes.TransitionProgress, 10);
        Assert.Equal("first", scenes.CurrentSceneName);
        AssertOverlay(gui, transition.Color, 0.5f);

        scenes.ProcessPendingSceneChange(0.25);
        Assert.Equal(0.5, scenes.TransitionProgress, 10);
        Assert.Equal("second", scenes.CurrentSceneName);
        AssertOverlay(gui, transition.Color, 1.0f);

        scenes.ProcessPendingSceneChange(0.25);
        Assert.Equal(0.75, scenes.TransitionProgress, 10);
        AssertOverlay(gui, transition.Color, 0.5f);

        scenes.ProcessPendingSceneChange(0.25);
        Assert.True(scenes.IsTransitioning);
        Assert.Equal(1.0, scenes.TransitionProgress, 10);
        Assert.True(runtime.IsPaused);
        Assert.True(gui.IsInputBlocked);

        scenes.CompleteTransitionFrame();
        Assert.False(scenes.IsTransitioning);
        Assert.False(runtime.IsPaused);
        Assert.False(gui.IsInputBlocked);
        AssertNoOverlay(gui);
    }

    [Fact]
    public void TransitionOverlay_RendersAbovePersistentAndSceneGui()
    {
        var gui = new GuiSystem(600.0f);
        var runtime = new RuntimeController();
        var scenes = CreateInitializedManager(gui, runtime);
        var persistent = gui.AddPanel();
        persistent.BackgroundColor = new Vector4D<float>(1.0f, 0.0f, 0.0f, 1.0f);
        var transitionColor = new Vector4D<float>(0.0f, 0.0f, 1.0f, 1.0f);
        scenes.LoadScene("second", new FadeToColorSceneTransition(transitionColor, 1.0));
        scenes.ProcessPendingSceneChange();
        scenes.ProcessPendingSceneChange(0.5);
        var renderer = new RecordingRenderContext();

        gui.Render(renderer);

        Assert.True(renderer.ScreenRectangles.Count >= 2);
        Assert.Equal(persistent.BackgroundColor, renderer.ScreenRectangles[^2].Color);
        Assert.Equal(transitionColor, renderer.ScreenRectangles[^1].Color);
        Assert.Equal(Vector2D<float>.Zero, renderer.ScreenRectangles[^1].Position);
        Assert.Equal(new Vector2D<float>(800.0f, 600.0f), renderer.ScreenRectangles[^1].Size);
    }

    [Fact]
    public void LargeFrameDelta_StillPresentsOpaqueMidpointBeforeFadeIn()
    {
        var gui = new GuiSystem();
        var runtime = new RuntimeController();
        var scenes = CreateInitializedManager(gui, runtime);
        var transition = new FadeToColorSceneTransition(Vector4D<float>.One, 0.4);
        scenes.LoadScene("second", transition);
        scenes.ProcessPendingSceneChange();

        scenes.ProcessPendingSceneChange(5.0);

        Assert.Equal("second", scenes.CurrentSceneName);
        Assert.Equal(0.5, scenes.TransitionProgress, 10);
        AssertOverlay(gui, transition.Color, 1.0f);

        scenes.ProcessPendingSceneChange(5.0);
        Assert.True(scenes.IsTransitioning);
        Assert.Equal(1.0, scenes.TransitionProgress, 10);

        scenes.CompleteTransitionFrame();
        Assert.False(scenes.IsTransitioning);
    }

    [Fact]
    public void FailedIncomingScene_ReleasesTransitionResourcesAndKeepsPreviousScene()
    {
        var gui = new GuiSystem();
        var runtime = new RuntimeController();
        var scenes = CreateInitializedManager(gui, runtime);
        scenes.RegisterScene("failing", () => new ThrowingSceneLogic());
        scenes.RegisterScene("recovery", () => new EmptySceneLogic());
        scenes.LoadScene("failing", new FadeToColorSceneTransition(Vector4D<float>.One, 0.4));
        scenes.ProcessPendingSceneChange();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            scenes.ProcessPendingSceneChange(0.2));

        Assert.Equal("Injected scene initialization failure.", exception.Message);
        Assert.Equal("first", scenes.CurrentSceneName);
        Assert.False(scenes.IsTransitioning);
        Assert.False(runtime.IsPaused);
        Assert.False(gui.IsInputBlocked);
        Assert.Single(gui.Layers);
        Assert.Same(scenes.CurrentScene!.Gui, gui.Layers[0]);
        AssertNoOverlay(gui);

        scenes.LoadScene("recovery");
        scenes.ProcessPendingSceneChange();
        Assert.Equal("recovery", scenes.CurrentSceneName);
    }

    [Fact]
    public void Shutdown_CancelsTransitionAndReleasesOverlayPauseAndInput()
    {
        var gui = new GuiSystem();
        var runtime = new RuntimeController();
        var scenes = CreateInitializedManager(gui, runtime);
        scenes.LoadScene("second", new FadeToColorSceneTransition(Vector4D<float>.One));
        scenes.ProcessPendingSceneChange();

        scenes.Shutdown();

        Assert.False(scenes.IsTransitioning);
        Assert.Null(scenes.CurrentScene);
        Assert.False(runtime.IsPaused);
        Assert.False(gui.IsInputBlocked);
        Assert.Empty(gui.Layers);
        AssertNoOverlay(gui);
    }

    [Fact]
    public void TransitionPause_RemainsAuthoritativeWhileOutgoingPausingScreenUnloads()
    {
        var gui = new GuiSystem();
        var runtime = new RuntimeController();
        var scenes = new SceneManager(gui, runtime);
        scenes.RegisterScene("paused", () => new CallbackSceneLogic(scene =>
            scene.Screens.Push(new GuiScreen("pause", new GuiPanel()) { PausesSimulation = true })));
        scenes.RegisterScene("next", () => new EmptySceneLogic());
        scenes.LoadScene("paused");
        scenes.Init();
        Assert.True(runtime.IsPaused);
        scenes.LoadScene("next", new FadeToColorSceneTransition(Vector4D<float>.One, 0.4));
        scenes.ProcessPendingSceneChange();

        scenes.ProcessPendingSceneChange(0.2);

        Assert.Equal("next", scenes.CurrentSceneName);
        Assert.True(runtime.IsPaused);

        scenes.ProcessPendingSceneChange(0.2);
        Assert.True(runtime.IsPaused);

        scenes.CompleteTransitionFrame();
        Assert.False(runtime.IsPaused);
    }

    [Fact]
    public void Transition_BlocksPreviouslyQueuedRuntimeSingleStep()
    {
        var gui = new GuiSystem();
        var runtime = new RuntimeController();
        var logic = new CountingSceneLogic();
        var scenes = new SceneManager(gui, runtime);
        scenes.RegisterScene("first", () => logic);
        scenes.RegisterScene("second", () => new EmptySceneLogic());
        scenes.LoadScene("first");
        scenes.Init();
        runtime.Pause();
        runtime.StepOneFrame();
        scenes.LoadScene("second", new FadeToColorSceneTransition(Vector4D<float>.One));
        scenes.ProcessPendingSceneChange();

        var timing = runtime.Advance(0.1);
        scenes.Update(timing.FrameDeltaTime);
        for (var step = 0; step < timing.FixedStepCount; step++)
            scenes.FixedUpdate(runtime.FixedDeltaTime);

        Assert.True(timing.ShouldUpdateSimulation);
        Assert.Equal(0, logic.UpdateCount);
        Assert.Equal(0, logic.FixedUpdateCount);
    }

    [Fact]
    public void InitialScene_IgnoresConfiguredTransition()
    {
        var gui = new GuiSystem();
        var runtime = new RuntimeController();
        var scenes = new SceneManager(gui, runtime);
        scenes.RegisterScene("first", () => new EmptySceneLogic());
        scenes.LoadScene("first", new FadeToColorSceneTransition(Vector4D<float>.One));

        scenes.Init();

        Assert.Equal("first", scenes.CurrentSceneName);
        Assert.False(scenes.IsTransitioning);
        Assert.False(runtime.IsPaused);
        Assert.False(gui.IsInputBlocked);
    }

    [Fact]
    public void PendingTransition_CanBeReplacedBeforeItStarts()
    {
        var gui = new GuiSystem();
        var runtime = new RuntimeController();
        var scenes = CreateInitializedManager(gui, runtime);
        scenes.RegisterScene("third", () => new EmptySceneLogic());
        scenes.LoadScene("second", new FadeToColorSceneTransition(Vector4D<float>.One));
        scenes.LoadScene("third", new FadeToColorSceneTransition(Vector4D<float>.One));

        scenes.ProcessPendingSceneChange();

        Assert.True(scenes.IsTransitioning);
        Assert.Equal("third", scenes.TransitionTargetSceneName);
        Assert.Throws<InvalidOperationException>(() => scenes.LoadScene("second"));
    }

    [Fact]
    public void Transition_BlocksGuiInputAndReleasesCapturedControl()
    {
        using var input = new InputManager();
        var gui = new GuiSystem(600.0f);
        var runtime = new RuntimeController();
        var scenes = CreateInitializedManager(gui, runtime);
        var button = gui.AddButton("PERSISTENT");
        var clicks = 0;
        button.Clicked += (_, _) => clicks++;
        gui.Render(new RecordingRenderContext());
        input.ProcessMouseMove(new System.Numerics.Vector2(2.0f, 2.0f));
        input.ProcessMouseDown(InputMouseButton.Left);
        gui.Update(input);
        Assert.True(gui.IsPointerCaptured);

        scenes.LoadScene("second", new FadeToColorSceneTransition(Vector4D<float>.One));
        scenes.ProcessPendingSceneChange();
        input.ProcessMouseUp(InputMouseButton.Left);
        gui.Update(input);

        Assert.False(gui.IsPointerCaptured);
        Assert.False(gui.IsPointerOverControl);
        Assert.Equal(0, clicks);
    }

    [Fact]
    public void TransitionDefinitions_RejectInvalidValuesAndBadEasingOutput()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new FadeToColorSceneTransition(Vector4D<float>.One, 0.0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new FadeToColorSceneTransition(new Vector4D<float>(float.NaN, 0.0f, 0.0f, 1.0f)));

        var gui = new GuiSystem();
        var runtime = new RuntimeController();
        var scenes = CreateInitializedManager(gui, runtime);
        scenes.LoadScene(
            "second",
            new FadeToColorSceneTransition(Vector4D<float>.One, 1.0, _ => double.NaN));

        Assert.Throws<InvalidOperationException>(() => scenes.ProcessPendingSceneChange());
        Assert.False(scenes.IsTransitioning);
        Assert.False(runtime.IsPaused);
        Assert.False(gui.IsInputBlocked);
    }

    [Fact]
    public void EasingFailureAfterTransitionStarts_ReleasesOwnedState()
    {
        var gui = new GuiSystem();
        var runtime = new RuntimeController();
        var scenes = CreateInitializedManager(gui, runtime);
        scenes.LoadScene(
            "second",
            new FadeToColorSceneTransition(
                Vector4D<float>.One,
                1.0,
                progress => progress == 0.0 ? 0.0 : double.NaN));
        scenes.ProcessPendingSceneChange();

        Assert.Throws<InvalidOperationException>(() => scenes.ProcessPendingSceneChange(0.1));

        Assert.Equal("first", scenes.CurrentSceneName);
        Assert.False(scenes.IsTransitioning);
        Assert.False(runtime.IsPaused);
        Assert.False(gui.IsInputBlocked);
        AssertNoOverlay(gui);
    }

    [Fact]
    public void TransitionUpdate_RejectsInvalidDeltaTime()
    {
        var scenes = CreateInitializedManager(new GuiSystem(), new RuntimeController());

        Assert.Throws<ArgumentOutOfRangeException>(() => scenes.ProcessPendingSceneChange(double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => scenes.ProcessPendingSceneChange(-0.1));
    }

    private static SceneManager CreateInitializedManager(GuiSystem gui, RuntimeController runtime)
    {
        var scenes = new SceneManager(gui, runtime);
        scenes.RegisterScene("first", () => new EmptySceneLogic());
        scenes.RegisterScene("second", () => new EmptySceneLogic());
        scenes.LoadScene("first");
        scenes.Init();
        return scenes;
    }

    private static void AssertOverlay(
        GuiSystem gui,
        Vector4D<float> expectedColor,
        float expectedOpacity)
    {
        var renderer = new RecordingRenderContext();
        gui.Render(renderer);
        var overlay = Assert.Single(renderer.ScreenRectangles);
        Assert.Equal(expectedColor.X, overlay.Color.X);
        Assert.Equal(expectedColor.Y, overlay.Color.Y);
        Assert.Equal(expectedColor.Z, overlay.Color.Z);
        Assert.Equal(expectedColor.W * expectedOpacity, overlay.Color.W, 5);
    }

    private static void AssertNoOverlay(GuiSystem gui)
    {
        var renderer = new RecordingRenderContext();
        gui.Render(renderer);
        Assert.Empty(renderer.ScreenRectangles);
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

    private sealed class ThrowingSceneLogic : SceneLogic
    {
        public override void Init() =>
            throw new InvalidOperationException("Injected scene initialization failure.");

        public override void Update(double dt)
        {
        }
    }

    private sealed class CountingSceneLogic : SceneLogic
    {
        internal int UpdateCount { get; private set; }
        internal int FixedUpdateCount { get; private set; }

        public override void Init()
        {
        }

        public override void Update(double dt) => UpdateCount++;

        public override void FixedUpdate(double dt) => FixedUpdateCount++;
    }

    private sealed class CallbackSceneLogic(Action<Scene> initialize) : SceneLogic
    {
        public override void Init() => initialize(Scene);

        public override void Update(double dt)
        {
        }
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
