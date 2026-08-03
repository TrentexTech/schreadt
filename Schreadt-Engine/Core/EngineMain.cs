using Schreadt_Engine.Asset;

using Schreadt_Engine.Component.Logic;

namespace Schreadt_Engine.Core;

internal static class EngineMain
{
    private static Application? app;
    private static AssetCatalog? assets;

    internal static void Init(GameLogic? gameLogic)
    {
        var initializationTimer = System.Diagnostics.Stopwatch.StartNew();
        EngineLog.Information(
            $"Initializing engine with game logic '{gameLogic?.GetType().FullName ?? "none"}'.",
            "Engine");

        try
        {
            Config.Load();
            EngineLog.Information(
                $"Loading {Config.Data.AssetLibraries.Count} configured asset library/libraries.",
                "Assets");
            assets = AssetCatalog.LoadFromDirectory(FileHandler.ContentRoot, Config.Data.AssetLibraries);
            EngineLog.Information($"Asset catalog ready with {assets.Count} asset(s).", "Assets");
            State.SetAssets(assets);
            app = new Application(gameLogic);
            app.Init();
            initializationTimer.Stop();
            EngineLog.Information(
                $"Engine initialized successfully in {initializationTimer.Elapsed.TotalMilliseconds:F1} ms.",
                "Engine");
        }
        catch (Exception exception)
        {
            EngineLog.Error("Engine initialization failed; disposing loaded asset resources.", exception, "Engine");
            assets?.Dispose();
            assets = null;
            app = null;
            State.SetAssets(null);
            throw;
        }
    }

    internal static void Start()
    {
        var runTimer = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            EngineLog.Information("Starting main loop.", "Engine");
            (app ?? throw new InvalidOperationException("The engine must be initialized before it can start.")).Start();
        }
        finally
        {
            runTimer.Stop();
            var frameCount = app?.Runtime.FrameCount ?? 0;
            assets?.Dispose();
            assets = null;
            State.SetAssets(null);
            app = null;
            EngineLog.Information(
                $"Engine stopped after {runTimer.Elapsed.TotalSeconds:F2} seconds and {frameCount} frame(s).",
                "Engine");
        }
    }
}
