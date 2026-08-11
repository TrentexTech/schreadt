using Schreadt_Engine.Component;
using Schreadt_Engine.Component.Logic;
using Schreadt_Engine.Gui;

namespace Schreadt_Engine.Core;

/// <summary>
/// Registers scene factories and owns the lifecycle of the active scene.
/// </summary>
public sealed class SceneManager
{
    private readonly Dictionary<string, Func<SceneLogic>> _sceneFactories = new(StringComparer.Ordinal);
    private readonly GuiSystem? _gui;
    private readonly RuntimeController? _runtime;
    private IEngineContext? _context;
    private string? _pendingSceneName;
    private bool _initialized;

    public Scene? CurrentScene { get; private set; }

    public string? CurrentSceneName => CurrentScene?.Name;

    public IReadOnlyCollection<string> RegisteredScenes => _sceneFactories.Keys;

    public event Action<Scene>? SceneLoaded;

    public event Action<Scene>? SceneUnloaded;

    public SceneManager(GuiSystem? gui = null, RuntimeController? runtime = null)
    {
        _gui = gui;
        _runtime = runtime;
    }

    public void RegisterScene(string name, Func<SceneLogic> factory)
    {
        ValidateSceneName(name);
        ArgumentNullException.ThrowIfNull(factory);

        if (!_sceneFactories.TryAdd(name, factory))
        {
            throw new InvalidOperationException($"A scene named '{name}' is already registered.");
        }

        EngineLog.Debug($"Registered scene '{name}'. Total registered scenes: {_sceneFactories.Count}.", "Scenes");
    }

    public bool IsSceneRegistered(string name)
    {
        ValidateSceneName(name);
        return _sceneFactories.ContainsKey(name);
    }

    /// <summary>
    /// Queues a scene to become active at the next safe scene-update boundary.
    /// The initial scene is applied while the manager initializes.
    /// </summary>
    public void LoadScene(string name)
    {
        ValidateSceneName(name);

        if (!_sceneFactories.ContainsKey(name))
        {
            throw new KeyNotFoundException($"No scene named '{name}' has been registered.");
        }

        var replacedRequest = _pendingSceneName;
        _pendingSceneName = name;
        EngineLog.Debug(
            replacedRequest is null
                ? $"Queued scene load '{name}'."
                : $"Replaced pending scene load '{replacedRequest}' with '{name}'.",
            "Scenes");
    }

    public void ReloadCurrentScene()
    {
        var currentName = CurrentSceneName
            ?? throw new InvalidOperationException("There is no current scene to reload.");

        LoadScene(currentName);
    }

    internal void Init()
    {
        if (_initialized) return;

        _initialized = true;
        EngineLog.Debug($"Scene manager initializing with {_sceneFactories.Count} registered scene(s).", "Scenes");
        ApplyPendingSceneChange();

        if (CurrentScene is null)
        {
            throw new InvalidOperationException(
                "No initial scene was loaded. Register and load a scene from GameLogic.Init().");
        }
    }

    internal void SetContext(IEngineContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (_context is not null)
            throw new InvalidOperationException("The scene manager is already attached to an engine context.");
        if (_initialized || CurrentScene is not null)
            throw new InvalidOperationException("The scene manager context must be assigned before initialization.");

        _context = context;
    }

    internal void Update(double dt)
    {
        if (!_initialized) throw new InvalidOperationException("The scene manager has not been initialized.");

        ApplyPendingSceneChange();
        CurrentScene!.Update(dt);
    }

    internal void ProcessPendingSceneChange()
    {
        if (!_initialized) throw new InvalidOperationException("The scene manager has not been initialized.");
        ApplyPendingSceneChange();
    }

    internal void FixedUpdate(double dt)
    {
        if (!_initialized) throw new InvalidOperationException("The scene manager has not been initialized.");

        CurrentScene!.FixedUpdate(dt);
        CurrentScene.Collisions.Step(dt);
    }

    internal void Shutdown()
    {
        _pendingSceneName = null;
        _initialized = false;

        if (CurrentScene is null) return;

        var scene = CurrentScene;
        CurrentScene = null;
        scene.Unload();
        _gui?.RemoveLayer(scene.Gui);
        EngineLog.Information($"Scene unloaded during shutdown: {scene.Name}.", "Scenes");
        SceneUnloaded?.Invoke(scene);
    }

    private void ApplyPendingSceneChange()
    {
        if (_pendingSceneName is null) return;

        var sceneName = _pendingSceneName;
        _pendingSceneName = null;
        var loadTimer = System.Diagnostics.Stopwatch.StartNew();
        EngineLog.Debug(
            $"Loading scene '{sceneName}' (previous: '{CurrentSceneName ?? "none"}').",
            "Scenes");

        var factory = _sceneFactories[sceneName];
        SceneLogic logic;
        try
        {
            logic = factory()
                ?? throw new InvalidOperationException($"The factory for scene '{sceneName}' returned null.");
        }
        catch (Exception exception)
        {
            EngineLog.Error($"Scene factory for '{sceneName}' failed.", exception, "Scenes");
            throw;
        }
        var nextScene = new Scene(sceneName, logic, _runtime, _context);
        var previousScene = CurrentScene;

        _gui?.AddLayer(nextScene.Gui);
        CurrentScene = nextScene;
        try
        {
            nextScene.Init();
        }
        catch (Exception exception)
        {
            EngineLog.Error($"Scene '{sceneName}' failed during initialization.", exception, "Scenes");
            CurrentScene = previousScene;
            nextScene.Unload();
            _gui?.RemoveLayer(nextScene.Gui);
            throw;
        }

        if (previousScene is not null)
        {
            previousScene.Unload();
            _gui?.RemoveLayer(previousScene.Gui);
            EngineLog.Information($"Scene unloaded: {previousScene.Name}.", "Scenes");
            SceneUnloaded?.Invoke(previousScene);
        }

        loadTimer.Stop();
        EngineLog.Information(
            $"Scene loaded: {nextScene.Name} using {logic.GetType().Name} in " +
            $"{loadTimer.Elapsed.TotalMilliseconds:F1} ms.",
            "Scenes");
        SceneLoaded?.Invoke(nextScene);
    }

    private static void ValidateSceneName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
    }
}
