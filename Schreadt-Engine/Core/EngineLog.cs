using System.Globalization;
using System.Text;

namespace Schreadt_Engine.Core;

public enum EngineLogLevel
{
    Trace,
    Debug,
    Information,
    Warning,
    Error,
    Fatal
}

public readonly record struct EngineLogEntry(
    DateTimeOffset Timestamp,
    EngineLogLevel Level,
    string Message,
    string? Category,
    Exception? Exception);

public static class EngineLog
{
    private static readonly object Sync = new();
    private static StreamWriter? _writer;

    public static EngineLogLevel MinimumLevel { get; set; } = EngineLogLevel.Information;

    public static string? CurrentLogFilePath { get; private set; }

    public static event Action<EngineLogEntry>? EntryWritten;

    public static void Trace(string message, string? category = null)
        => Write(EngineLogLevel.Trace, message, null, category);

    public static void Debug(string message, string? category = null)
        => Write(EngineLogLevel.Debug, message, null, category);

    public static void Information(string message, string? category = null)
        => Write(EngineLogLevel.Information, message, null, category);

    public static void Info(string message, string? category = null)
        => Information(message, category);

    public static void Warning(string message, string? category = null)
        => Write(EngineLogLevel.Warning, message, null, category);

    public static void Error(string message, Exception? exception = null, string? category = null)
        => Write(EngineLogLevel.Error, message, exception, category);

    public static void Fatal(string message, Exception? exception = null, string? category = null)
        => Write(EngineLogLevel.Fatal, message, exception, category);

    public static void Write(
        EngineLogLevel level,
        string message,
        Exception? exception = null,
        string? category = null)
    {
        if (!Enum.IsDefined(level)) throw new ArgumentOutOfRangeException(nameof(level));
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        if (level < MinimumLevel) return;

        var entry = new EngineLogEntry(
            DateTimeOffset.Now,
            level,
            message.Trim(),
            string.IsNullOrWhiteSpace(category) ? null : category.Trim(),
            exception);
        var formatted = Format(entry);

        lock (Sync)
        {
            try
            {
                _writer?.WriteLine(formatted);
                _writer?.Flush();
            }
            catch
            {
                // Logging must never replace the original engine failure.
            }
        }

        try
        {
            if (level >= EngineLogLevel.Warning) Console.Error.WriteLine(formatted);
            else Console.Out.WriteLine(formatted);
        }
        catch
        {
            // A redirected or unavailable console must not affect the engine.
        }

        try
        {
            EntryWritten?.Invoke(entry);
        }
        catch
        {
        }
    }

    internal static void Initialize(string? logDirectory = null)
    {
        string? initializationWarning = null;
        lock (Sync)
        {
            if (_writer is not null) return;

            var requestedDirectory = string.IsNullOrWhiteSpace(logDirectory)
                ? Path.Combine(AppContext.BaseDirectory, "logs")
                : Path.GetFullPath(logDirectory);

            try
            {
                OpenLogFile(requestedDirectory);
            }
            catch (Exception exception)
            {
                var fallbackDirectory = Path.Combine(Path.GetTempPath(), "Schreadt Engine", "logs");
                try
                {
                    OpenLogFile(fallbackDirectory);
                    initializationWarning =
                        $"The requested log directory '{requestedDirectory}' was unavailable; " +
                        $"using '{fallbackDirectory}' instead. {exception.Message}";
                }
                catch (Exception fallbackException)
                {
                    CurrentLogFilePath = null;
                    initializationWarning =
                        $"Logging could not create a log file in '{requestedDirectory}' or '{fallbackDirectory}'. " +
                        $"{exception.Message} {fallbackException.Message}";
                }
            }
        }

        if (initializationWarning is not null) Warning(initializationWarning, "Logging");
        Information(
            $"Logging initialized. Minimum level: {MinimumLevel}; " +
            $"File: {CurrentLogFilePath ?? "unavailable"}.",
            "Engine");
        Information(
            $"Runtime: {Environment.Version}; OS: {Environment.OSVersion}; " +
            $"64-bit process: {Environment.Is64BitProcess}; Process: {Environment.ProcessId}; " +
            $"Base directory: {AppContext.BaseDirectory}",
            "Environment");
    }

    internal static void Shutdown()
    {
        if (_writer is not null) Information("Logging is shutting down.", "Logging");

        lock (Sync)
        {
            try
            {
                _writer?.Flush();
                _writer?.Dispose();
            }
            catch
            {
            }
            finally
            {
                _writer = null;
                CurrentLogFilePath = null;
            }
        }
    }

    internal static string Format(EngineLogEntry entry)
    {
        var builder = new StringBuilder();
        builder.Append(entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture));
        builder.Append(" [").Append(entry.Level.ToString().ToUpperInvariant()).Append(']');
        if (entry.Category is not null) builder.Append(" [").Append(entry.Category).Append(']');
        builder.Append(' ').Append(entry.Message);
        if (entry.Exception is not null) builder.AppendLine().Append(entry.Exception);
        return builder.ToString();
    }

    private static void OpenLogFile(string directory)
    {
        Directory.CreateDirectory(directory);
        var fileName = FormattableString.Invariant(
            $"schreadt-{DateTimeOffset.Now:yyyyMMdd-HHmmss-fff}-{Environment.ProcessId}.log");
        CurrentLogFilePath = Path.Combine(directory, fileName);
        _writer = new StreamWriter(
            new FileStream(CurrentLogFilePath, FileMode.Create, FileAccess.Write, FileShare.Read),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }
}
