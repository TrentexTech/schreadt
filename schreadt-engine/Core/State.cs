namespace Schreadt_Engine.Core;

public static class State
{
    private static Reality? _currentReality;
    private static InputManager? _input;

    public static string[] LaunchArgs { get; internal set; } = [];

    public static Reality CurrentReality => _currentReality
                                            ?? throw new InvalidOperationException("The engine has not initialized its reality yet.");

    public static InputManager Input => _input
                                        ?? throw new InvalidOperationException("The engine has not initialized input yet.");

    internal static void SetCurrentReality(Reality reality)
    {
        _currentReality = reality;
    }

    internal static void SetInput(InputManager input)
    {
        _input = input;
    }
}
