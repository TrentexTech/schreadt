using Schreadt_Engine.Core;

namespace Schreadt_Engine;

public static class EntryPoint
{
    public static void Main(string[] args)
    {
        State.LaunchArgs = args;

        EngineMain.Init();
        EngineMain.Start();
    }
}