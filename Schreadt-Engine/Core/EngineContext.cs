using Schreadt_Engine.Asset;
using Schreadt_Engine.Component;
using Schreadt_Engine.Gui;

namespace Schreadt_Engine.Core;

/// <summary>
/// Services owned by one running engine instance. Gameplay receives this
/// context through its engine lifecycle instead of resolving process-wide state.
/// </summary>
public interface IEngineContext
{
    IReadOnlyList<string> LaunchArgs { get; }
    IInputService Input { get; }
    IAssetProvider Assets { get; }
    IWindowController Window { get; }
    SceneManager Scenes { get; }
    RuntimeController Runtime { get; }
    GuiSystem Gui { get; }
    Camera MainCamera { get; }
}

internal sealed class EngineContext : IEngineContext
{
    private readonly Reality _reality;

    internal EngineContext(
        IEnumerable<string> launchArgs,
        IInputService input,
        IAssetProvider assets,
        IWindowController window,
        Reality reality,
        RuntimeController runtime,
        GuiSystem gui)
    {
        ArgumentNullException.ThrowIfNull(launchArgs);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(assets);
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(reality);
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(gui);

        LaunchArgs = Array.AsReadOnly(launchArgs.ToArray());
        Input = input;
        Assets = assets;
        Window = window;
        _reality = reality;
        Runtime = runtime;
        Gui = gui;
    }

    public IReadOnlyList<string> LaunchArgs { get; }
    public IInputService Input { get; }
    public IAssetProvider Assets { get; }
    public IWindowController Window { get; }
    public SceneManager Scenes => _reality.Scenes;
    public RuntimeController Runtime { get; }
    public GuiSystem Gui { get; }
    public Camera MainCamera => _reality.MainCamera;
}
