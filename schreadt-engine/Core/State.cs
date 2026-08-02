namespace Schreadt_Engine.Core;

public static class State
{
    private static Reality? _currentReality;

    public static string[] LaunchArgs { get; internal set; } = [];

    public static Reality CurrentReality => _currentReality
                                            ?? throw new InvalidOperationException("The engine has not initialized its reality yet.");

    internal static void SetCurrentReality(Reality reality)
    {
        _currentReality = reality;
    }
}
