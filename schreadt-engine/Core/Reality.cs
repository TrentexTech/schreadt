using Schreadt_Engine.Component;
using Schreadt_Engine.Component.Logic;

namespace Schreadt_Engine.Core;

public class Reality : IUpdateable
{
    private bool _initialized;

    public GameLogic? GameLogic { get; }
    public SceneManager Scenes { get; }
    public Scene Scene => Scenes.CurrentScene
        ?? throw new InvalidOperationException("No scene is currently loaded.");
    public Camera MainCamera { get; private set; }

    public Camera Camera => MainCamera;

    internal Reality(GameLogic? gameLogic)
    {
        Scenes = new SceneManager();
        MainCamera = new Camera();
        GameLogic = gameLogic;
        GameLogic?.Attach(this);
    }

    internal void Init()
    {
        GameLogic?.Init();
        Scenes.Init();
        MainCamera.Init();
        _initialized = true;
    }

    public void Update(double dt)
    {
        GameLogic?.Update(dt);
        Scenes.Update(dt);
        MainCamera.Update(dt);
    }

    public void Render(Renderer renderer)
    {
        renderer.Render(MainCamera, Scene);
    }

    internal void Shutdown()
    {
        Scenes.Shutdown();
    }

    public void SetMainCamera(Camera camera)
    {
        ArgumentNullException.ThrowIfNull(camera);

        if (_initialized && !camera.Initialized) camera.Init();
        MainCamera = camera;
    }
}
