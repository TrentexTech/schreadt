using Schreadt_Engine.Component;
using Schreadt_Engine.Component.Logic;
using Schreadt_Engine.Gui;
using Silk.NET.Maths;

namespace Schreadt_Engine.Core;

/// <summary>
/// Registers scene factories and owns the lifecycle of the active scene.
/// </summary>
public sealed class SceneManager
{
    private readonly Dictionary<string, Func<SceneLogic>> _sceneFactories = new(StringComparer.Ordinal);
    private readonly GuiSystem? _gui;
    private readonly RuntimeController? _runtime;
    private readonly SceneTransitionOverlay? _transitionOverlay;
    private IEngineContext? _context;
    private PendingSceneLoad? _pendingSceneLoad;
    private ActiveSceneTransition? _activeTransition;
    private bool _transitionOverlayAttached;
    private bool _transitionPauseRequested;
    private bool _transitionInputBlocked;
    private bool _initialized;

    public Scene? CurrentScene { get; private set; }

    public string? CurrentSceneName => CurrentScene?.Name;

    public IReadOnlyCollection<string> RegisteredScenes => _sceneFactories.Keys;

    public bool IsTransitioning => _activeTransition is not null;

    public SceneTransition? ActiveTransition => _activeTransition?.Transition;

    public string? TransitionTargetSceneName => _activeTransition?.TargetSceneName;

    public double TransitionProgress => _activeTransition?.Progress ?? 0.0;

    public event Action<Scene>? SceneLoaded;

    public event Action<Scene>? SceneUnloaded;

    public SceneManager(GuiSystem? gui = null, RuntimeController? runtime = null)
    {
        _gui = gui;
        _runtime = runtime;
        if (gui is not null) _transitionOverlay = new SceneTransitionOverlay { Visible = false };
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
        QueueSceneLoad(name, transition: null);
    }

    /// <summary>
    /// Queues a scene change wrapped in an unscaled visual transition. If there is no active scene yet,
    /// initialization loads the requested scene immediately without playing the transition.
    /// </summary>
    public void LoadScene(string name, SceneTransition transition)
    {
        ArgumentNullException.ThrowIfNull(transition);
        QueueSceneLoad(name, transition);
    }

    private void QueueSceneLoad(string name, SceneTransition? transition)
    {
        ValidateSceneName(name);

        if (!_sceneFactories.ContainsKey(name))
        {
            throw new KeyNotFoundException($"No scene named '{name}' has been registered.");
        }

        if (_activeTransition is not null)
            throw new InvalidOperationException("A scene load cannot be queued while another scene transition is active.");

        var replacedRequest = _pendingSceneLoad;
        _pendingSceneLoad = new PendingSceneLoad(name, transition);
        EngineLog.Debug(
            replacedRequest is null
                ? transition is null
                    ? $"Queued scene load '{name}'."
                    : $"Queued scene load '{name}' using {transition.GetType().Name}."
                : $"Replaced pending scene load '{replacedRequest.SceneName}' with '{name}'.",
            "Scenes");
    }

    public void ReloadCurrentScene()
    {
        var currentName = CurrentSceneName
            ?? throw new InvalidOperationException("There is no current scene to reload.");

        LoadScene(currentName);
    }

    public void ReloadCurrentScene(SceneTransition transition)
    {
        ArgumentNullException.ThrowIfNull(transition);
        var currentName = CurrentSceneName
            ?? throw new InvalidOperationException("There is no current scene to reload.");

        LoadScene(currentName, transition);
    }

    internal void Init()
    {
        if (_initialized) return;

        _initialized = true;
        EngineLog.Debug($"Scene manager initializing with {_sceneFactories.Count} registered scene(s).", "Scenes");
        AttachTransitionOverlay();
        try
        {
            ApplyPendingSceneChangeImmediately();

            if (CurrentScene is null)
            {
                throw new InvalidOperationException(
                    "No initial scene was loaded. Register and load a scene from GameLogic.Init().");
            }
        }
        catch
        {
            _initialized = false;
            DetachTransitionOverlay();
            throw;
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

        ProcessPendingSceneChange(0.0);
        if (_activeTransition is not null) return;
        CurrentScene!.Update(dt);
    }

    internal void ProcessPendingSceneChange() => ProcessPendingSceneChange(0.0);

    internal void ProcessPendingSceneChange(double unscaledDeltaTime)
    {
        if (!_initialized) throw new InvalidOperationException("The scene manager has not been initialized.");
        if (!double.IsFinite(unscaledDeltaTime) || unscaledDeltaTime < 0.0)
            throw new ArgumentOutOfRangeException(
                nameof(unscaledDeltaTime),
                "Scene transition delta time must be finite and non-negative.");

        if (_activeTransition is not null)
        {
            if (_activeTransition.AwaitingCompletion) return;
            try
            {
                AdvanceTransition(unscaledDeltaTime);
            }
            catch
            {
                CancelTransition("transition update failure");
                throw;
            }
            return;
        }

        if (_pendingSceneLoad is null) return;
        if (_pendingSceneLoad.Transition is null || CurrentScene is null)
        {
            ApplyPendingSceneChangeImmediately();
            return;
        }

        var request = _pendingSceneLoad;
        _pendingSceneLoad = null;
        StartTransition(request);
    }

    internal void FixedUpdate(double dt)
    {
        if (!_initialized) throw new InvalidOperationException("The scene manager has not been initialized.");
        if (_activeTransition is not null) return;

        CurrentScene!.FixedUpdate(dt);
        CurrentScene.Collisions.Step(dt);
    }

    internal void CompleteTransitionFrame()
    {
        var active = _activeTransition;
        if (active is null || !active.AwaitingCompletion) return;

        EngineLog.Information(
            $"Scene transition completed: '{active.SourceSceneName ?? "none"}' -> '{active.TargetSceneName}'.",
            "Scenes");
        _activeTransition = null;
        FinishTransitionResources();
    }

    internal void Shutdown()
    {
        _pendingSceneLoad = null;
        CancelTransition("engine shutdown");
        _initialized = false;

        try
        {
            if (CurrentScene is null) return;

            var scene = CurrentScene;
            CurrentScene = null;
            scene.Unload();
            _gui?.RemoveLayer(scene.Gui);
            EngineLog.Information($"Scene unloaded during shutdown: {scene.Name}.", "Scenes");
            SceneUnloaded?.Invoke(scene);
        }
        finally
        {
            DetachTransitionOverlay();
        }
    }

    private void ApplyPendingSceneChangeImmediately()
    {
        if (_pendingSceneLoad is null) return;

        var sceneName = _pendingSceneLoad.SceneName;
        _pendingSceneLoad = null;
        ApplySceneChange(sceneName);
    }

    private void ApplySceneChange(string sceneName)
    {
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

    private void StartTransition(PendingSceneLoad request)
    {
        var transition = request.Transition
            ?? throw new InvalidOperationException("A scene transition request did not contain a transition.");
        var active = new ActiveSceneTransition(CurrentSceneName, request.SceneName, transition);
        _activeTransition = active;

        try
        {
            if (_runtime is not null)
            {
                _runtime.AcquirePauseRequest();
                _transitionPauseRequested = true;
            }

            if (_gui is not null)
            {
                _gui.AcquireInputBlock();
                _transitionInputBlocked = true;
            }

            UpdateTransitionOverlay(active);
        }
        catch
        {
            FinishTransitionResources();
            _activeTransition = null;
            throw;
        }

        EngineLog.Information(
            $"Scene transition started: '{active.SourceSceneName ?? "none"}' -> '{active.TargetSceneName}' " +
            $"using {transition.GetType().Name} ({transition.Duration:G4} s).",
            "Scenes");
    }

    private void AdvanceTransition(double unscaledDeltaTime)
    {
        var active = _activeTransition!;
        if (unscaledDeltaTime == 0.0)
        {
            UpdateTransitionOverlay(active);
            return;
        }

        active.Elapsed = Math.Min(active.Transition.PhaseDuration, active.Elapsed + unscaledDeltaTime);
        UpdateTransitionOverlay(active);
        if (active.Elapsed < active.Transition.PhaseDuration) return;

        if (active.Phase == SceneTransitionPhase.FadeOut)
        {
            try
            {
                ApplySceneChange(active.TargetSceneName);
            }
            catch
            {
                CancelTransition("scene load failure");
                throw;
            }

            active.Phase = SceneTransitionPhase.FadeIn;
            active.Elapsed = 0.0;
            UpdateTransitionOverlay(active);
            return;
        }

        active.AwaitingCompletion = true;
    }

    private void CancelTransition(string reason)
    {
        var active = _activeTransition;
        if (active is null) return;

        _activeTransition = null;
        EngineLog.Warning(
            $"Scene transition cancelled ({reason}): '{active.SourceSceneName ?? "none"}' -> " +
            $"'{active.TargetSceneName}'.",
            "Scenes");
        FinishTransitionResources();
    }

    private void UpdateTransitionOverlay(ActiveSceneTransition active)
    {
        if (_transitionOverlay is null) return;
        _transitionOverlay.Color = active.Transition.CreateOverlayColor(active.Progress);
        _transitionOverlay.Visible = true;
    }

    private void FinishTransitionResources()
    {
        if (_transitionOverlay is not null)
        {
            _transitionOverlay.Color = Vector4D<float>.Zero;
            _transitionOverlay.Visible = false;
        }

        if (_transitionInputBlocked && _gui is not null)
        {
            try
            {
                _gui.ReleaseInputBlock();
            }
            catch (Exception exception)
            {
                EngineLog.Error("Could not release scene-transition GUI input blocking.", exception, "Scenes");
            }
            finally
            {
                _transitionInputBlocked = false;
            }
        }

        if (_transitionPauseRequested && _runtime is not null)
        {
            try
            {
                _runtime.ReleasePauseRequest();
            }
            catch (Exception exception)
            {
                EngineLog.Error("Could not release the scene-transition simulation pause.", exception, "Scenes");
            }
            finally
            {
                _transitionPauseRequested = false;
            }
        }
    }

    private void AttachTransitionOverlay()
    {
        if (_gui is null || _transitionOverlay is null || _transitionOverlayAttached) return;
        _gui.AddOverlay(_transitionOverlay);
        _transitionOverlayAttached = true;
    }

    private void DetachTransitionOverlay()
    {
        if (_gui is null || _transitionOverlay is null || !_transitionOverlayAttached) return;
        _gui.RemoveOverlay(_transitionOverlay);
        _transitionOverlayAttached = false;
    }

    private static void ValidateSceneName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
    }

    private sealed record PendingSceneLoad(string SceneName, SceneTransition? Transition);

    private sealed class ActiveSceneTransition(
        string? sourceSceneName,
        string targetSceneName,
        SceneTransition transition)
    {
        internal string? SourceSceneName { get; } = sourceSceneName;
        internal string TargetSceneName { get; } = targetSceneName;
        internal SceneTransition Transition { get; } = transition;
        internal SceneTransitionPhase Phase { get; set; }
        internal double Elapsed { get; set; }
        internal bool AwaitingCompletion { get; set; }
        internal double Progress => Phase == SceneTransitionPhase.FadeOut
            ? 0.5 * Elapsed / Transition.PhaseDuration
            : 0.5 + (0.5 * Elapsed / Transition.PhaseDuration);
    }

    private enum SceneTransitionPhase
    {
        FadeOut,
        FadeIn
    }
}
