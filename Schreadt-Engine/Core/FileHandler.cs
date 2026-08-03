namespace Schreadt_Engine.Core;

internal static class FileHandler
{
    internal static string ContentRoot { get; } = AppContext.BaseDirectory;

    internal static string ReadFileAsString(FileType fileType)
    {
        var path = GetPath(fileType);

        if (!File.Exists(path)) throw new FileNotFoundException($"Required engine file was not found: '{path}'.", path);
        return File.ReadAllText(path);
    }

    internal static string GetPath(FileType fileType)
    {
        return fileType switch
        {
            FileType.GameConfig => Path.Combine(ContentRoot, "config", "config.json"),
            _ => throw new ArgumentOutOfRangeException(nameof(fileType), fileType, "Unsupported engine file type.")
        };
    }
}

internal enum FileType
{
    GameConfig
}
