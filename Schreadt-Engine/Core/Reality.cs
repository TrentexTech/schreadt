using Schreadt_Engine.Component;
using Schreadt_Engine.Component.Logic;
using Schreadt_Engine.Gui;

namespace Schreadt_Engine.Core;

public class Reality : IUpdateable
{
    private bool _initialized;

    public GameLogic? GameLogic { get; }
    public SceneManager Scenes { get; }
    public Scene Scene => Scenes.CurrentScene
        ?? throw new InvalidOperationException("No scene is currently loaded.");
    public Camera MainCamera { get; private set; }

    internal Reality(GameLogic? gameLogic, GuiSystem? gui = null, RuntimeController? runtime = null)
    {
        Scenes = new SceneManager(gui, runtime);
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
        UpdateGameplay(dt);
        UpdateCamera(dt);
    }

    internal void UpdateGameplay(double dt)
    {
        GameLogic?.Update(dt);
        Scenes.Update(dt);
    }

    internal void ProcessPendingSceneChange()
    {
        Scenes.ProcessPendingSceneChange();
    }

    internal void FixedUpdate(double dt)
    {
        GameLogic?.FixedUpdate(dt);
        Scenes.FixedUpdate(dt);
    }

    internal void UpdateCamera(double dt)
    {
        MainCamera.Update(dt);
    }

    public void Render(IRenderer2D renderer, GuiSystem? gui = null)
    {
        renderer.Render(MainCamera, Scene, gui);
    }

    internal void Shutdown()
    {
        Scenes.Shutdown();
        MainCamera.Shutdown();
        GameLogic?.Shutdown();
        _initialized = false;
    }

    public void SetMainCamera(Camera camera)
    {
        ArgumentNullException.ThrowIfNull(camera);

        if (_initialized && !camera.Initialized) camera.Init();
        MainCamera = camera;
    }
}
