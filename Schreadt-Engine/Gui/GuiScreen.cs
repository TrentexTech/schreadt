using Schreadt_Engine.Core;

namespace Schreadt_Engine.Gui;

public sealed class GuiScreen
{
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

    public bool PausesSimulation { get; set; }

    public bool DismissOnEscape { get; set; }

    public bool IsOpen { get; internal set; }

    public event Action<GuiScreen>? Opened;

    public event Action<GuiScreen>? Closed;

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

    internal GuiScreenStack(GuiLayer layer)
    {
        _layer = layer;
    }

    public IReadOnlyList<GuiScreen> Screens => _screens;

    public GuiScreen? Top => _screens.Count == 0 ? null : _screens[^1];

    public event Action<GuiScreen>? ScreenPushed;

    public event Action<GuiScreen>? ScreenRemoved;

    public GuiScreen Push(GuiScreen screen)
    {
        ArgumentNullException.ThrowIfNull(screen);
        if (screen.IsOpen || _screens.Contains(screen))
            throw new InvalidOperationException("The GUI screen is already open.");
        if (_layer.Elements.Any(element => ReferenceEquals(element, screen.Root)) ||
            _screens.Any(candidate => ReferenceEquals(candidate.Root, screen.Root)))
        {
            throw new InvalidOperationException("The screen root is already registered on this GUI layer.");
        }
        if (_screens.Any(candidate => string.Equals(candidate.Name, screen.Name, StringComparison.Ordinal)))
            throw new InvalidOperationException($"A GUI screen named '{screen.Name}' is already open.");

        _screens.Add(screen);
        screen.NotifyOpened();
        ScreenPushed?.Invoke(screen);
        UpdateSimulationPause();
        return screen;
    }

    public GuiScreen? Pop()
    {
        if (_screens.Count == 0) return null;
        var screen = _screens[^1];
        RemoveAt(_screens.Count - 1, screen);
        return screen;
    }

    public bool Remove(GuiScreen screen)
    {
        ArgumentNullException.ThrowIfNull(screen);
        var index = _screens.IndexOf(screen);
        if (index < 0) return false;
        RemoveAt(index, screen);
        return true;
    }

    public bool Remove(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
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
        for (var index = _screens.Count - 1; index >= 0; index--)
        {
            var screen = _screens[index];
            _layer.ReleaseInteraction(screen.Root);
            _screens.RemoveAt(index);
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

    private void RemoveAt(int index, GuiScreen screen)
    {
        _layer.ReleaseInteraction(screen.Root);
        _screens.RemoveAt(index);
        screen.NotifyClosed();
        ScreenRemoved?.Invoke(screen);
        UpdateSimulationPause();
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
}
