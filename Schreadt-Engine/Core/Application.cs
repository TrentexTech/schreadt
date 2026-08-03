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
        Input = new InputManager();
        State.SetInput(Input);
        Gui = new GuiSystem();
        State.SetGui(Gui);
        _performanceOverlay = new PerformanceOverlay(Gui);
        Reality = new Reality(gameLogic);
        State.SetCurrentReality(Reality);
        Runtime = new RuntimeController();
        State.SetRuntime(Runtime);
        Window = new Window(this);
        State.SetWindow(Window);
    }

    internal void Init()
    {
        Reality.Init();
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
        Reality.Shutdown();
    }
}
