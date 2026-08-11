using Schreadt_Engine.Core;

using Schreadt_Engine.Component.Logic;

namespace Schreadt_Engine;

public static class EntryPoint
{
    public static void Main(string[] args) => Start(args, null);

    public static void Run(GameLogic gameLogic, string[] args)
    {
        ArgumentNullException.ThrowIfNull(gameLogic);
        Start(args, gameLogic);
    }

    private static void Start(string[] args, GameLogic? gameLogic)
    {
        EngineLog.Initialize();
        try
        {
            EngineMain.Init(gameLogic, args);
            EngineMain.Start();
        }
        catch (Exception exception)
        {
            var logFilePath = EngineLog.CurrentLogFilePath;
            EngineLog.Fatal("The engine terminated because of an unhandled exception.", exception, "Engine");
            FatalErrorPresenter.Show(exception, logFilePath);
            Environment.ExitCode = 1;
        }
        finally
        {
            EngineLog.Shutdown();
        }
    }
}
