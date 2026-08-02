namespace Schreadt_Engine.Core;

internal static class FileHandler
{
    private static string workingDir = Directory.GetCurrentDirectory();

    internal static string? ReadFileAsString(FileType fileType, string? extra = null)
    {
        var path = Path.Combine(workingDir, GetFilePathFor(fileType, extra));

        if (!File.Exists(path)) return null;

        try
        {
            return File.ReadAllText(path);
        }
        catch (Exception e)
        {
            // TODO: Handle missing file.
            throw;
        }
    }

    private static string GetFilePathFor(FileType fileType, string? extra = null)
    {
        switch (fileType)
        {
            case FileType.GameConfig: return "." + Path.DirectorySeparatorChar + "config" + Path.DirectorySeparatorChar + "config.json";
            case FileType.Manifest: return "." + Path.DirectorySeparatorChar + "assets" + Path.DirectorySeparatorChar + extra + ".json";
            default: throw new NotImplementedException($"File type {fileType} not implemented");
        }
    }
}

internal enum FileType
{
    GameConfig,
    Asset,
    Manifest
}