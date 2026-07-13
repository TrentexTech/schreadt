using Schreadt_Engine.Component;
using Schreadt_Engine.Component.Logic;

namespace Schreadt_Engine.Core;

public class Reality : IUpdateable
{
    public GameLogic? GameLogic;
    public Scene Scene;
    public Camera Camera;

    internal Reality()
    {
        Camera = new Camera();
        Scene = new Scene(0);
    }

    internal void Init()
    {
        GameLogic?.Init();
        Scene.Init();
        Camera.Init();
    }

    public void Update(double dt)
    {
        GameLogic?.Update(dt);
        Scene.Update(dt);
        Camera.Update(dt);
    }

    public void Render(Renderer renderer)
    {
        renderer.Render(Camera, Scene);
    }
}