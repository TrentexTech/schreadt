namespace Schreadt_Engine.Asset;

public abstract class AssetLibrary : IAssetSource
{
    private bool _initialized;

    protected AssetLibraryManifest Manifest { get; private set; } = null!;

    public string Name => Manifest.Name;

    internal static AssetLibrary Create(AssetLibraryManifest manifest)
    {
        var type = FindLibraryType(manifest.Type)
                   ?? throw new InvalidDataException(
                       $"Asset library type '{manifest.Type}' from manifest '{manifest.ManifestPath}' could not be found.");

        if (!typeof(AssetLibrary).IsAssignableFrom(type) || type.IsAbstract)
            throw new InvalidDataException($"Type '{manifest.Type}' is not a concrete {nameof(AssetLibrary)}.");

        if (Activator.CreateInstance(type) is not AssetLibrary library)
            throw new InvalidDataException($"Asset library type '{manifest.Type}' must have a public parameterless constructor.");

        library.Manifest = manifest;
        library._initialized = true;
        return library;
    }

    public IReadOnlyCollection<AssetRecord> LoadAssets()
    {
        if (!_initialized) throw new InvalidOperationException("Asset libraries must be created from a manifest.");
        return Load();
    }

    protected abstract IReadOnlyCollection<AssetRecord> Load();

    public virtual void Dispose()
    {
    }

    private static Type? FindLibraryType(string classQualifier)
    {
        var type = Type.GetType(classQualifier, false, true);
        if (type is not null) return type;

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            type = assembly.GetType(classQualifier, false, true);
            if (type is not null) return type;
        }

        return null;
    }
}
