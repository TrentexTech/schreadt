using Schreadt_Engine.Component.Logic;
using Schreadt_Engine.Gui;

namespace Schreadt_Engine.Core;

internal class Application
{
    private readonly PerformanceOverlay _performanceOverlay;
    private bool _shutdown;

    internal readonly Window Window;
    internal readonly InputManager Input;
    internal readonly Reality Reality;
    internal readonly GuiSystem Gui;
    internal readonly RuntimeController Runtime;

    internal Application(GameLogic? gameLogic)
    {
        EngineLog.Debug("Constructing engine subsystems.", "Application");
        Input = new InputManager();
        State.SetInput(Input);
        Gui = new GuiSystem(Config.Data.Window.DefaultSize.Height);
        State.SetGui(Gui);
        _performanceOverlay = new PerformanceOverlay(Gui);
        Runtime = new RuntimeController();
        State.SetRuntime(Runtime);
        Reality = new Reality(gameLogic, Gui, Runtime);
        State.SetCurrentReality(Reality);
        Window = new Window(this);
        State.SetWindow(Window);
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
            Gui.Update(Input);
            if (_shutdown || Window.IsCloseRequested) return;

            Reality.ProcessPendingSceneChange();

            var timing = Runtime.Advance(dt);
            if (!timing.ShouldUpdateSimulation) return;

            Reality.UpdateGameplay(timing.FrameDeltaTime);

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

    internal void Render(IRenderer2D renderer, double frameTime)
    {
        if (_shutdown || Window.IsCloseRequested) return;

        _performanceOverlay.Update(frameTime);
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
