using Schreadt_Engine.Component.Logic;
using Schreadt_Engine.Gui;

namespace Schreadt_Engine.Core;

internal class Application
{
    private readonly FixedStepClock _physicsClock = new();
    private readonly PerformanceOverlay _performanceOverlay;

    internal readonly Window Window;
    internal readonly InputManager Input;
    internal readonly Reality Reality;
    internal readonly GuiSystem Gui;

    internal Application(GameLogic? gameLogic)
    {
        Input = new InputManager();
        State.SetInput(Input);
        Gui = new GuiSystem();
        State.SetGui(Gui);
        _performanceOverlay = new PerformanceOverlay(Gui);
        Reality = new Reality(gameLogic);
        State.SetCurrentReality(Reality);
        Window = new Window(this);
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
            var timing = _physicsClock.Advance(dt);

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
        _performanceOverlay.Update(frameTime);
        Reality.Render(renderer, Gui);
    }

    internal void Shutdown()
    {
        Reality.Shutdown();
    }
}
