namespace Schreadt_Engine.Asset;

using Schreadt_Engine.Core;

public sealed class LooseAssetLibrary : AssetLibrary
{
    protected override IReadOnlyCollection<AssetRecord> Load()
    {
        var manifestDirectory = Path.GetDirectoryName(Manifest.ManifestPath)
                                ?? throw new InvalidDataException($"Manifest '{Manifest.ManifestPath}' has no parent directory.");
        var root = Path.GetFullPath(Path.Combine(manifestDirectory, Manifest.Root));
        EnsureContained(manifestDirectory, root, "root", Manifest.Root);
        EngineLog.Debug(
            $"Loading loose asset library '{Name}' from '{root}' with {Manifest.Assets.Count} manifest entry/entries.",
            "Assets");

        var loaded = new List<AssetRecord>(Manifest.Assets.Count);
        foreach (var entry in Manifest.Assets)
        {
            var path = Path.GetFullPath(Path.Combine(root, entry.Path));
            EnsureContained(root, path, "asset", entry.Path);

            if (!File.Exists(path))
                throw new FileNotFoundException(
                    $"Asset '{entry.Id}' from library '{Name}' was not found at '{path}'.",
                    path);

            loaded.Add(new AssetRecord(entry.Id, entry.ContentType, path, File.ReadAllBytes(path)));
            EngineLog.Trace($"Loaded asset '{entry.Id}' from '{path}'.", "Assets");
        }

        return loaded.AsReadOnly();
    }

    private static void EnsureContained(string root, string path, string kind, string configuredPath)
    {
        var relative = Path.GetRelativePath(root, path);
        if (relative == ".." || relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) || Path.IsPathRooted(relative))
            throw new InvalidDataException(
                $"Configured {kind} path '{configuredPath}' escapes its asset library directory.");
    }
}
