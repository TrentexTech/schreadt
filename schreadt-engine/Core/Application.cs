namespace Schreadt_Engine.Core;

internal class Application
{
    internal readonly Window Window;
    internal readonly InputManager Input;
    internal Reality Reality;

    internal Application()
    {
        Input = new InputManager();
        State.SetInput(Input);
        Reality = new Reality();
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
            Reality.Update(dt);
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
}
