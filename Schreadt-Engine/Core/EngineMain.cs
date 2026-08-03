using Schreadt_Engine.Asset;

using Schreadt_Engine.Component.Logic;

namespace Schreadt_Engine.Core;

internal static class EngineMain
{
    private static Application? app;
    private static AssetCatalog? assets;

    internal static void Init(GameLogic? gameLogic)
    {
        EngineLog.Information("Initializing engine.", "Engine");
        Config.Load();
        EngineLog.Information("Loading asset libraries.", "Assets");
        assets = AssetCatalog.LoadFromDirectory(FileHandler.ContentRoot, Config.Data.AssetLibraries);

        try
        {
            State.SetAssets(assets);
            app = new Application(gameLogic);
            app.Init();
            EngineLog.Information("Engine initialized successfully.", "Engine");
        }
        catch
        {
            assets.Dispose();
            assets = null;
            State.SetAssets(null);
            throw;
        }
    }

    internal static void Start()
    {
        try
        {
            EngineLog.Information("Starting main loop.", "Engine");
            (app ?? throw new InvalidOperationException("The engine must be initialized before it can start.")).Start();
        }
        finally
        {
            assets?.Dispose();
            assets = null;
            State.SetAssets(null);
            app = null;
            EngineLog.Information("Engine stopped.", "Engine");
        }
    }
}
