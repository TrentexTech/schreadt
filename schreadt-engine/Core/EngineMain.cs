using Schreadt_Engine.Asset;

using Schreadt_Engine.Component.Logic;

namespace Schreadt_Engine.Core;

internal static class EngineMain
{
    private static Application? app;

    internal static void Init(GameLogic? gameLogic)
    {
        Config.Load();
        Assets.Init();
        app = new Application(gameLogic);
        app.Init();
    }

    internal static void Start()
    {
        Assets.Load();
        (app ?? throw new InvalidOperationException("The engine must be initialized before it can start.")).Start();
    }
}
