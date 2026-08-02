using Schreadt_Engine.Component;
using Schreadt_Engine.Component.Logic;

namespace Schreadt_Engine.Core;

public class Reality : IUpdateable
{
    private bool _initialized;

    public GameLogic? GameLogic { get; }
    public Scene Scene;
    public Camera MainCamera { get; private set; }

    public Camera Camera => MainCamera;

    internal Reality(GameLogic? gameLogic)
    {
        GameLogic = gameLogic;
        GameLogic?.Attach(this);
        MainCamera = new Camera();
        Scene = new Scene(0);
    }

    internal void Init()
    {
        GameLogic?.Init();
        Scene.Init();
        MainCamera.Init();
        _initialized = true;
    }

    public void Update(double dt)
    {
        GameLogic?.Update(dt);
        Scene.Update(dt);
        MainCamera.Update(dt);
    }

    public void Render(Renderer renderer)
    {
        renderer.Render(MainCamera, Scene);
    }

    public void SetMainCamera(Camera camera)
    {
        ArgumentNullException.ThrowIfNull(camera);

        if (_initialized && !camera.Initialized) camera.Init();
        MainCamera = camera;
    }
}
