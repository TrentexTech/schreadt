using Schreadt_Engine.Component.Logic;

namespace Schreadt_Engine.Core;

internal class Application
{
    private readonly FixedStepClock _physicsClock = new();

    internal readonly Window Window;
    internal readonly InputManager Input;
    internal readonly Reality Reality;

    internal Application(GameLogic? gameLogic)
    {
        Input = new InputManager();
        State.SetInput(Input);
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

    internal void Render(Renderer renderer)
    {
        Reality.Render(renderer);
    }

    internal void Shutdown()
    {
        Reality.Shutdown();
    }
}
