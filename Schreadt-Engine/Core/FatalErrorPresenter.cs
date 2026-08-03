using Silk.NET.SDL;

namespace Schreadt_Engine.Core;

internal static class FatalErrorPresenter
{
    private const uint ErrorMessageBoxFlag = 0x00000010u;

    internal static string CreateMessage(Exception exception, string? logFilePath)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var summary = exception.Message.Trim();
        if (summary.Length == 0) summary = "An unexpected error occurred.";
        if (summary.Length > 1200) summary = summary[..1200] + "…";

        var message = $"The game encountered a fatal error and must close.\n\n{summary}";
        return string.IsNullOrWhiteSpace(logFilePath)
            ? message + "\n\nAdditional details were written to the console."
            : message + $"\n\nTechnical details were written to:\n{logFilePath}";
    }

    internal static unsafe bool Show(Exception exception, string? logFilePath)
    {
        var message = CreateMessage(exception, logFilePath);
        try
        {
            using var sdl = SdlApiLoader.GetApi();
            return sdl.ShowSimpleMessageBox(
                ErrorMessageBoxFlag,
                "Schreadt Engine - Fatal Error",
                message,
                null) == 0;
        }
        catch (Exception presentationException)
        {
            EngineLog.Error("SDL could not display the fatal-error dialog.", presentationException, "FatalError");
            try
            {
                Console.Error.WriteLine(message);
            }
            catch
            {
            }

            return false;
        }
    }
}
