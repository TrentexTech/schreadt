using Newtonsoft.Json;

namespace Schreadt_Engine.Asset;

public sealed class AssetCatalog : IDisposable
{
    private readonly Dictionary<string, AssetRecord> _assets = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ImageAsset> _images = new(StringComparer.Ordinal);
    private readonly List<AssetLibrary> _libraries = [];
    private bool _disposed;

    public int Count
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _assets.Count;
        }
    }

    public IReadOnlyCollection<string> Ids
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _assets.Keys;
        }
    }

    private AssetCatalog()
    {
    }

    public static AssetCatalog LoadFromDirectory(string contentRoot, IEnumerable<string> manifestNames)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentRoot);
        ArgumentNullException.ThrowIfNull(manifestNames);

        var catalog = new AssetCatalog();
        try
        {
            var assetsDirectory = Path.Combine(Path.GetFullPath(contentRoot), "assets");
            foreach (var manifestName in manifestNames)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(manifestName);
                if (Path.GetFileName(manifestName) != manifestName)
                    throw new ArgumentException($"Manifest name '{manifestName}' must not contain path separators.", nameof(manifestNames));

                var manifestPath = Path.Combine(assetsDirectory, $"{manifestName}.json");
                var manifest = AssetLibraryManifest.Load(manifestPath);
                var library = AssetLibrary.Create(manifest);
                catalog._libraries.Add(library);

                foreach (var asset in library.LoadAssets())
                {
                    if (!catalog._assets.TryAdd(asset.Id, asset))
                        throw new InvalidDataException(
                            $"Asset id '{asset.Id}' from library '{library.Name}' is already provided by another library.");
                }
            }

            return catalog;
        }
        catch
        {
            catalog.Dispose();
            throw;
        }
    }

    public bool Contains(string id)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _assets.ContainsKey(AssetId.Normalize(id));
    }

    public AssetRecord Get(string id)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var normalizedId = AssetId.Normalize(id);
        return _assets.TryGetValue(normalizedId, out var asset)
            ? asset
            : throw new KeyNotFoundException($"No asset with id '{normalizedId}' is loaded.");
    }

    public ReadOnlyMemory<byte> GetBytes(string id)
    {
        return Get(id).Data;
    }

    public string GetText(string id)
    {
        return Get(id).GetText();
    }

    public T GetJson<T>(string id)
    {
        var asset = Get(id);
        try
        {
            var value = JsonConvert.DeserializeObject<T>(asset.GetText());
            return value ?? throw new InvalidDataException($"JSON asset '{asset.Id}' deserialized to null.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"Asset '{asset.Id}' does not contain valid JSON for {typeof(T).Name}.", exception);
        }
    }

    public ImageAsset GetImage(string id)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var asset = Get(id);
        if (_images.TryGetValue(asset.Id, out var image)) return image;

        image = ImageAsset.Decode(asset);
        _images.Add(asset.Id, image);
        return image;
    }

    public void Dispose()
    {
        if (_disposed) return;

        foreach (var library in _libraries) library.Dispose();
        _libraries.Clear();
        _images.Clear();
        _assets.Clear();
        _disposed = true;
    }
}
