using Schreadt_Engine.Asset;
using Schreadt_Engine.Gui;

namespace Schreadt_Engine.Core;

public static class State
{
    private static Reality? _currentReality;
    private static InputManager? _input;
    private static GuiSystem? _gui;
    private static AssetCatalog? _assets;

    public static string[] LaunchArgs { get; internal set; } = [];

    public static Reality CurrentReality => _currentReality
                                            ?? throw new InvalidOperationException("The engine has not initialized its reality yet.");

    public static InputManager Input => _input
                                        ?? throw new InvalidOperationException("The engine has not initialized input yet.");

    public static GuiSystem Gui => _gui
                                   ?? throw new InvalidOperationException("The engine has not initialized its GUI yet.");

    public static AssetCatalog Assets => _assets
                                         ?? throw new InvalidOperationException("The engine has not loaded its assets yet.");

    internal static void SetCurrentReality(Reality reality)
    {
        _currentReality = reality;
    }

    internal static void SetInput(InputManager input)
    {
        _input = input;
    }

    internal static void SetGui(GuiSystem gui)
    {
        _gui = gui;
    }

    internal static void SetAssets(AssetCatalog? assets)
    {
        _assets = assets;
    }
}
