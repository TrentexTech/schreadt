using Schreadt_Engine.Asset;

using Schreadt_Engine.Component.Logic;

namespace Schreadt_Engine.Core;

internal static class EngineMain
{
    private static Application? app;
    private static AssetCatalog? assets;

    internal static void Init(GameLogic? gameLogic, IEnumerable<string>? launchArgs = null)
    {
        if (app is not null || assets is not null)
            throw new InvalidOperationException("The engine is already initialized.");

        var initializationTimer = System.Diagnostics.Stopwatch.StartNew();
        EngineLog.Information(
            $"Initializing engine with game logic '{gameLogic?.GetType().FullName ?? "none"}'.",
            "Engine");

        AssetCatalog? candidateAssets = null;
        Application? candidateApp = null;
        try
        {
            Config.Load();
            EngineLog.Information(
                $"Loading {Config.Data.AssetLibraries.Count} configured asset library/libraries.",
                "Assets");
            candidateAssets = AssetCatalog.LoadFromDirectory(FileHandler.ContentRoot, Config.Data.AssetLibraries);
            EngineLog.Information($"Asset catalog ready with {candidateAssets.Count} asset(s).", "Assets");
            candidateApp = new Application(gameLogic, candidateAssets, launchArgs ?? []);
            candidateApp.Init();
            assets = candidateAssets;
            app = candidateApp;
            initializationTimer.Stop();
            EngineLog.Information(
                $"Engine initialized successfully in {initializationTimer.Elapsed.TotalMilliseconds:F1} ms.",
                "Engine");
        }
        catch (Exception exception)
        {
            EngineLog.Error("Engine initialization failed; rolling back engine state and resources.", exception, "Engine");
            app = null;
            assets = null;
            RollBackInitialization(candidateApp, candidateAssets);
            throw;
        }
    }

    internal static void Start()
    {
        var currentApp = app ?? throw new InvalidOperationException("The engine must be initialized before it can start.");
        var runTimer = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            EngineLog.Information("Starting main loop.", "Engine");
            currentApp.Start();
        }
        finally
        {
            runTimer.Stop();
            var frameCount = currentApp.Runtime.FrameCount;
            Shutdown();
            EngineLog.Information(
                $"Engine stopped after {runTimer.Elapsed.TotalSeconds:F2} seconds and {frameCount} frame(s).",
                "Engine");
        }
    }

    internal static void Shutdown()
    {
        var currentApp = app;
        var currentAssets = assets;
        app = null;
        assets = null;

        try
        {
            if (currentApp is not null)
            {
                currentApp.Shutdown();
                currentApp.Input.Dispose();
            }
        }
        finally
        {
            currentAssets?.Dispose();
        }
    }

    private static void RollBackInitialization(
        Application? candidateApp,
        AssetCatalog? candidateAssets)
    {
        try
        {
            candidateApp?.Shutdown();
        }
        catch (Exception exception)
        {
            EngineLog.Error("Application rollback failed during shutdown.", exception, "Engine");
        }

        try
        {
            candidateApp?.Input.Dispose();
        }
        catch (Exception exception)
        {
            EngineLog.Error("Application rollback failed while disposing input.", exception, "Engine");
        }

        try
        {
            candidateAssets?.Dispose();
        }
        catch (Exception exception)
        {
            EngineLog.Error("Application rollback failed while disposing assets.", exception, "Engine");
        }
    }
}
