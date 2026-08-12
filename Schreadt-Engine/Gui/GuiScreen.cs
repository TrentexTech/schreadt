using Schreadt_Engine.Core;
using Silk.NET.Maths;

namespace Schreadt_Engine.Gui;

public sealed class GuiScreen
{
    private bool _pausesSimulation;

    public GuiScreen(string name, IGuiElement root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(root);
        Name = name.Trim();
        Root = root;
    }

    public string Name { get; }

    public IGuiElement Root { get; }

    public bool IsModal { get; set; } = true;

    public bool PausesSimulation
    {
        get => _pausesSimulation;
        set
        {
            if (_pausesSimulation == value) return;
            _pausesSimulation = value;
            PausesSimulationChanged?.Invoke(this);
        }
    }

    public bool DismissOnEscape { get; set; }

    /// <summary>Optional transition used by <see cref="GuiScreenStack.Push(GuiScreen)"/>.</summary>
    public GuiScreenTransition? OpeningTransition { get; set; }

    /// <summary>Optional transition used by <see cref="GuiScreenStack.Pop()"/>.</summary>
    public GuiScreenTransition? ClosingTransition { get; set; }

    public bool IsOpen { get; internal set; }

    public event Action<GuiScreen>? Opened;

    public event Action<GuiScreen>? Closed;

    internal event Action<GuiScreen>? PausesSimulationChanged;

    internal void NotifyOpened()
    {
        IsOpen = true;
        Opened?.Invoke(this);
    }

    internal void NotifyClosed()
    {
        IsOpen = false;
        Closed?.Invoke(this);
    }
}

public sealed class GuiScreenStack
{
    private readonly GuiLayer _layer;
    private readonly List<GuiScreen> _screens = [];
    private RuntimeController? _runtime;
    private bool _pauseRequested;
    private bool _preparingTransition;
    private ActiveGuiScreenTransition? _activeTransition;

    internal GuiScreenStack(GuiLayer layer)
    {
        _layer = layer;
    }

    public IReadOnlyList<GuiScreen> Screens => _screens;

    public GuiScreen? Top => _screens.Count == 0 ? null : _screens[^1];

    public bool IsTransitioning => _activeTransition is not null;

    public event Action<GuiScreen>? ScreenPushed;

    public event Action<GuiScreen>? ScreenRemoved;

    public event Action? TransitionStarted;

    public event Action? TransitionCompleted;

    public GuiScreen Push(GuiScreen screen)
    {
        ArgumentNullException.ThrowIfNull(screen);
        if (screen.OpeningTransition is not null) return Push(screen, screen.OpeningTransition);
        EnsureNotTransitioning();
        return PushCore(screen);
    }

    public GuiScreen Push(GuiScreen screen, GuiScreenTransition transition)
    {
        ArgumentNullException.ThrowIfNull(transition);
        EnsureNotTransitioning();
        var outgoing = Top;
        var incoming = PushForTransition(screen);
        StartTransition(outgoing, incoming, transition, isOpening: true, completion: null);
        return incoming;
    }

    private GuiScreen PushCore(GuiScreen screen)
    {
        ArgumentNullException.ThrowIfNull(screen);
        if (screen.IsOpen || _screens.Contains(screen))
            throw new InvalidOperationException("The GUI screen is already open.");
        if (_screens.Any(candidate => string.Equals(candidate.Name, screen.Name, StringComparison.Ordinal)))
            throw new InvalidOperationException($"A GUI screen named '{screen.Name}' is already open.");

        GuiElementOwnership.Claim(screen.Root, screen, $"GUI screen '{screen.Name}'");
        try
        {
            _screens.Add(screen);
            screen.PausesSimulationChanged += OnPausesSimulationChanged;
        }
        catch
        {
            _screens.Remove(screen);
            screen.PausesSimulationChanged -= OnPausesSimulationChanged;
            GuiElementOwnership.Release(screen.Root, screen);
            throw;
        }

        screen.NotifyOpened();
        ScreenPushed?.Invoke(screen);
        UpdateSimulationPause();
        return screen;
    }

    public GuiScreen? Pop()
    {
        if (Top?.ClosingTransition is { } transition) return Pop(transition);
        EnsureNotTransitioning();
        if (_screens.Count == 0) return null;
        var screen = _screens[^1];
        RemoveAt(_screens.Count - 1, screen);
        return screen;
    }

    public GuiScreen? Pop(GuiScreenTransition transition)
    {
        ArgumentNullException.ThrowIfNull(transition);
        EnsureNotTransitioning();
        if (_screens.Count == 0) return null;

        var outgoing = _screens[^1];
        var incoming = _screens.Count > 1 ? _screens[^2] : null;
        StartTransition(
            outgoing,
            incoming,
            transition,
            isOpening: false,
            () => RemoveAt(_screens.IndexOf(outgoing), outgoing));
        return outgoing;
    }

    public GuiScreen ReplaceTop(GuiScreen screen, GuiScreenTransition transition)
    {
        ArgumentNullException.ThrowIfNull(screen);
        ArgumentNullException.ThrowIfNull(transition);
        EnsureNotTransitioning();
        if (_screens.Count == 0) return Push(screen, transition);

        var outgoing = _screens[^1];
        var incoming = PushForTransition(screen);
        StartTransition(
            outgoing,
            incoming,
            transition,
            isOpening: true,
            () => RemoveAt(_screens.IndexOf(outgoing), outgoing));
        return incoming;
    }

    public bool Remove(GuiScreen screen)
    {
        ArgumentNullException.ThrowIfNull(screen);
        EnsureNotTransitioning();
        var index = _screens.IndexOf(screen);
        if (index < 0) return false;
        RemoveAt(index, screen);
        return true;
    }

    public bool Remove(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        EnsureNotTransitioning();
        var index = _screens.FindLastIndex(screen => string.Equals(screen.Name, name.Trim(), StringComparison.Ordinal));
        if (index < 0) return false;
        RemoveAt(index, _screens[index]);
        return true;
    }

    public bool Contains(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _screens.Any(screen => string.Equals(screen.Name, name.Trim(), StringComparison.Ordinal));
    }

    public void Clear()
    {
        if (_preparingTransition)
            throw new InvalidOperationException("The GUI screen stack cannot be cleared from a transition opening callback.");
        if (_activeTransition is not null)
            EngineLog.Debug("Cancelled the active GUI screen transition while clearing its stack.", "GUI");
        _activeTransition = null;
        for (var index = _screens.Count - 1; index >= 0; index--)
        {
            var screen = _screens[index];
            _layer.ReleaseInteraction(screen.Root);
            _screens.RemoveAt(index);
            screen.PausesSimulationChanged -= OnPausesSimulationChanged;
            GuiElementOwnership.Release(screen.Root, screen);
            screen.NotifyClosed();
            ScreenRemoved?.Invoke(screen);
        }

        UpdateSimulationPause();
    }

    internal void SetRuntime(RuntimeController? runtime)
    {
        if (ReferenceEquals(_runtime, runtime)) return;
        ReleaseSimulationPause();
        _runtime = runtime;
        UpdateSimulationPause();
    }

    internal void UpdateTransition(double unscaledDeltaTime)
    {
        if (!double.IsFinite(unscaledDeltaTime) || unscaledDeltaTime < 0.0)
            throw new ArgumentOutOfRangeException(
                nameof(unscaledDeltaTime),
                "Transition delta time must be finite and non-negative.");

        var active = _activeTransition;
        if (active is null || unscaledDeltaTime == 0.0) return;

        active.Elapsed = Math.Min(active.Transition.Duration, active.Elapsed + unscaledDeltaTime);
        if (active.Elapsed < active.Transition.Duration) return;

        _activeTransition = null;
        EngineLog.Debug(
            $"Completed {active.Transition.GetType().Name} from " +
            $"'{active.Outgoing?.Name ?? "none"}' to '{active.Incoming?.Name ?? "none"}'.",
            "GUI");
        active.Completion?.Invoke();
        TransitionCompleted?.Invoke();
    }

    internal GuiScreenPresentation GetPresentation(GuiScreen screen, Vector2D<float> viewportSize)
    {
        var active = _activeTransition;
        if (active is null) return GuiScreenPresentation.Default;

        var progress = active.Elapsed / active.Transition.Duration;
        var frame = active.Transition.CreateFrame(progress, active.IsOpening, viewportSize);
        if (ReferenceEquals(screen, active.Outgoing))
            return new GuiScreenPresentation(frame.OutgoingOpacity, frame.OutgoingOffset);
        if (ReferenceEquals(screen, active.Incoming))
            return new GuiScreenPresentation(frame.IncomingOpacity, frame.IncomingOffset);
        return GuiScreenPresentation.Default;
    }

    internal Vector4D<float> GetTransitionOverlay(Vector2D<float> viewportSize)
    {
        var active = _activeTransition;
        if (active is null) return Vector4D<float>.Zero;
        var progress = active.Elapsed / active.Transition.Duration;
        return active.Transition.CreateFrame(progress, active.IsOpening, viewportSize).OverlayColor;
    }

    private void RemoveAt(int index, GuiScreen screen)
    {
        _layer.ReleaseInteraction(screen.Root);
        _screens.RemoveAt(index);
        screen.PausesSimulationChanged -= OnPausesSimulationChanged;
        GuiElementOwnership.Release(screen.Root, screen);
        screen.NotifyClosed();
        ScreenRemoved?.Invoke(screen);
        UpdateSimulationPause();
    }

    private void StartTransition(
        GuiScreen? outgoing,
        GuiScreen? incoming,
        GuiScreenTransition transition,
        bool isOpening,
        Action? completion)
    {
        foreach (var screen in _screens) _layer.ReleaseInteraction(screen.Root);
        _activeTransition = new ActiveGuiScreenTransition(
            outgoing,
            incoming,
            transition,
            isOpening,
            completion);
        EngineLog.Debug(
            $"Started {transition.GetType().Name} from '{outgoing?.Name ?? "none"}' to " +
            $"'{incoming?.Name ?? "none"}' ({transition.Duration:G4} s).",
            "GUI");
        TransitionStarted?.Invoke();
    }

    private GuiScreen PushForTransition(GuiScreen screen)
    {
        _preparingTransition = true;
        try
        {
            return PushCore(screen);
        }
        finally
        {
            _preparingTransition = false;
        }
    }

    private void EnsureNotTransitioning()
    {
        if (_activeTransition is not null || _preparingTransition)
            throw new InvalidOperationException("The GUI screen stack is already running a transition.");
    }

    private void OnPausesSimulationChanged(GuiScreen screen)
    {
        if (_screens.Contains(screen)) UpdateSimulationPause();
    }

    private void UpdateSimulationPause()
    {
        var shouldPause = _screens.Any(screen => screen.PausesSimulation);
        if (shouldPause)
        {
            if (_runtime is not null && !_pauseRequested)
            {
                _runtime.AcquirePauseRequest();
                _pauseRequested = true;
            }

            return;
        }

        ReleaseSimulationPause();
    }

    private void ReleaseSimulationPause()
    {
        if (_pauseRequested && _runtime is not null) _runtime.ReleasePauseRequest();
        _pauseRequested = false;
    }

    private sealed class ActiveGuiScreenTransition(
        GuiScreen? outgoing,
        GuiScreen? incoming,
        GuiScreenTransition transition,
        bool isOpening,
        Action? completion)
    {
        internal GuiScreen? Outgoing { get; } = outgoing;
        internal GuiScreen? Incoming { get; } = incoming;
        internal GuiScreenTransition Transition { get; } = transition;
        internal bool IsOpening { get; } = isOpening;
        internal Action? Completion { get; } = completion;
        internal double Elapsed { get; set; }
    }
}

internal readonly record struct GuiScreenPresentation(float Opacity, Vector2D<float> Offset)
{
    internal static GuiScreenPresentation Default { get; } = new(1.0f, Vector2D<float>.Zero);
}
