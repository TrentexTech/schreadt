using Schreadt_Engine.Component;
using Schreadt_Engine.Component.Logic;
using Schreadt_Engine.Gui;

namespace Schreadt_Engine.Core;

public class Reality : IUpdateable
{
    private readonly FrameComposer2D _frameComposer = new();
    private bool _initialized;
    private IEngineContext? _context;

    public GameLogic? GameLogic { get; }
    public IEngineContext Context => _context
        ?? throw new InvalidOperationException("The reality has not been attached to an engine context.");
    public SceneManager Scenes { get; }
    public Scene Scene => Scenes.CurrentScene
        ?? throw new InvalidOperationException("No scene is currently loaded.");
    public Camera MainCamera { get; private set; }
    public FrameCompositionStatistics CompositionStatistics => _frameComposer.Statistics;

    internal Reality(GameLogic? gameLogic, GuiSystem? gui = null, RuntimeController? runtime = null)
    {
        Scenes = new SceneManager(gui, runtime);
        MainCamera = new Camera();
        GameLogic = gameLogic;
        GameLogic?.Attach(this);
    }

    internal void AttachContext(IEngineContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (_context is not null)
            throw new InvalidOperationException("The reality is already attached to an engine context.");

        _context = context;
        Scenes.SetContext(context);
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
        if (Scenes.IsTransitioning) return;
        GameLogic?.Update(dt);
        Scenes.Update(dt);
    }

    internal void ProcessPendingSceneChange(double unscaledDeltaTime)
    {
        Scenes.ProcessPendingSceneChange(unscaledDeltaTime);
    }

    internal void FixedUpdate(double dt)
    {
        if (Scenes.IsTransitioning) return;
        GameLogic?.FixedUpdate(dt);
        Scenes.FixedUpdate(dt);
    }

    internal void CompleteSceneTransitionFrame()
    {
        Scenes.CompleteTransitionFrame();
    }

    internal void UpdateCamera(double dt)
    {
        if (Scenes.IsTransitioning) return;
        MainCamera.Update(dt);
    }

    public void Render(IFrameRenderer2D renderer, GuiSystem? gui = null)
    {
        _frameComposer.ComposeFrame(renderer, MainCamera, Scene, gui);
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
