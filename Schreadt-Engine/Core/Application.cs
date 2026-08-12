using Schreadt_Engine.Component.Logic;
using Schreadt_Engine.Asset;
using Schreadt_Engine.Gui;

namespace Schreadt_Engine.Core;

internal class Application
{
    private readonly PerformanceOverlay _performanceOverlay;
    private bool _shutdown;
    private int _lastFixedStepCount;

    internal readonly Window Window;
    internal readonly InputManager Input;
    internal readonly Reality Reality;
    internal readonly GuiSystem Gui;
    internal readonly RuntimeController Runtime;
    internal readonly IEngineContext Context;

    internal Application(GameLogic? gameLogic, IAssetProvider assets, IEnumerable<string> launchArgs)
    {
        ArgumentNullException.ThrowIfNull(assets);
        ArgumentNullException.ThrowIfNull(launchArgs);

        EngineLog.Debug("Constructing engine subsystems.", "Application");
        Input = new InputManager();
        Gui = new GuiSystem(Config.Data.Window.DefaultSize.Height);
        _performanceOverlay = new PerformanceOverlay(Gui);
        Runtime = new RuntimeController();
        Reality = new Reality(gameLogic, Gui, Runtime);
        Window = new Window(this, assets);
        Context = new EngineContext(launchArgs, Input, assets, Window, Reality, Runtime, Gui);
        Reality.AttachContext(Context);
        EngineLog.Debug("Input, GUI, runtime, reality, and window subsystems constructed.", "Application");
    }

    internal void Init()
    {
        EngineLog.Debug("Initializing reality and initial scene.", "Application");
        Reality.Init();
        EngineLog.Information(
            $"Application initialized with scene '{Reality.Scenes.CurrentSceneName}' and " +
            $"{Reality.Scenes.RegisteredScenes.Count} registered scene(s).",
            "Application");
    }

    internal void Start()
    {
        Window.Run();
    }

    internal void Update(double dt)
    {
        try
        {
            _performanceOverlay.HandleInput(Input);
            Gui.Update(Input, dt);
            if (_shutdown || Window.IsCloseRequested) return;

            Reality.ProcessPendingSceneChange(dt);

            var sceneTransitionWasActive = Reality.Scenes.IsTransitioning;
            var timing = Runtime.Advance(dt);
            Reality.CompleteSceneTransitionFrame();
            _lastFixedStepCount = sceneTransitionWasActive ? 0 : timing.FixedStepCount;
            if (sceneTransitionWasActive || !timing.ShouldUpdateSimulation) return;

            Reality.UpdateGameplay(timing.FrameDeltaTime);
            if (Reality.Scenes.IsTransitioning)
            {
                _lastFixedStepCount = 0;
                return;
            }

            for (var step = 0; step < timing.FixedStepCount; step++)
            {
                Reality.FixedUpdate(FixedStepClock.FixedDeltaTime);
            }

            Reality.UpdateCamera(timing.FrameDeltaTime);
        }
        finally
        {
            Input.EndFrame();
        }
    }

    internal void Render(IFrameRenderer2D renderer, double frameTime)
    {
        if (_shutdown || Window.IsCloseRequested) return;

        if (_performanceOverlay.IsVisible)
        {
            _performanceOverlay.Update(
                frameTime,
                Runtime,
                _lastFixedStepCount,
                Reality.Scene.Collisions.Statistics,
                renderer.Statistics,
                PerformanceDisplayMetrics.Create(
                    Window.Size,
                    Window.DisplayState,
                    Window.VSync,
                    Window.FramebufferSize,
                    renderer.ViewportOffset,
                    renderer.ViewportSize,
                    Gui));
        }
        Reality.Render(renderer, Gui);
    }

    internal void Shutdown()
    {
        if (_shutdown) return;

        _shutdown = true;
        EngineLog.Information(
            $"Application shutdown started at frame {Runtime.FrameCount}; active scene: " +
            $"'{Reality.Scenes.CurrentSceneName ?? "none"}'.",
            "Application");
        Reality.Shutdown();
        EngineLog.Information("Application shutdown completed.", "Application");
    }
}
