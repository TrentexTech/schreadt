using Schreadt_Engine.Core;

namespace Schreadt_Engine.Asset;

internal static class Assets
{
    private static readonly List<AssetLibraryManifest> _manifests = [];
    private static readonly List<AssetLibrary> _libraries = [];

    internal static void Init()
    {
        if (Config.Data.AssetLibraries is null) throw new Exception("Asset library list in game config does not exist!");

        foreach (var manifestName in Config.Data.AssetLibraries)
        {
            var manifest = AssetLibraryManifest.ManifestFromName(manifestName);
            if (manifest != null) _manifests.Add(manifest);
            else Console.Error.WriteLine($"Could not create manifest '{manifestName}'!");
        }

        foreach (var manifest in _manifests)
        {
            var library = AssetLibrary.LibraryFromManifest(manifest);
            if (library != null) _libraries.Add(library);
            else Console.Error.WriteLine($"Could not create asset library '{manifest.Name}'!");
        }
    }

    internal static void Load()
    {
        foreach (var library in _libraries)
        {
            library.Load();
        }
    }
}