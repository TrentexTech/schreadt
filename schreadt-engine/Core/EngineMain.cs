using Schreadt_Engine.Asset;

namespace Schreadt_Engine.Core;

internal static class EngineMain
{
    private static Application app;

    internal static void Init()
    {
        Config.Load();
        Assets.Init();
        app = new Application();
        app.Init();
    }

    internal static void Start()
    {
        Assets.Load();
        app.Start();
    }
}