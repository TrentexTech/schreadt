namespace Schreadt_Engine.Core;

internal class Application
{
    internal readonly Window Window;
    internal Reality Reality;

    internal Application()
    {
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
        Reality.Update(dt);
    }

    internal void Render(Renderer renderer)
    {
        Reality.Render(renderer);
    }
}
