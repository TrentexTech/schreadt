namespace Schreadt_Engine.Asset;

public sealed class AssetCatalog : IAssetProvider, IDisposable
{
    private readonly record struct DecodedAssetKey(string Id, Type AssetType);

    private readonly Dictionary<string, AssetRecord> _assets = new(StringComparer.Ordinal);
    private readonly Dictionary<Type, List<object>> _decoders = [];
    private readonly Dictionary<DecodedAssetKey, object> _decodedAssets = [];
    private readonly List<IAssetSource> _sources = [];
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
        RegisterDecoder(new ImageAssetDecoder());
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
                catalog.AddSource(AssetLibrary.Create(manifest));
            }

            return catalog;
        }
        catch
        {
            catalog.Dispose();
            throw;
        }
    }

    public static AssetCatalog LoadFromSources(IEnumerable<IAssetSource> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);

        var catalog = new AssetCatalog();
        try
        {
            foreach (var source in sources)
            {
                ArgumentNullException.ThrowIfNull(source);
                catalog.AddSource(source);
            }

            return catalog;
        }
        catch
        {
            catalog.Dispose();
            throw;
        }
    }

    public void RegisterDecoder<T>(IAssetDecoder<T> decoder, bool replaceExisting = false)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(decoder);

        if (!_decoders.TryGetValue(typeof(T), out var registrations))
        {
            registrations = [];
            _decoders.Add(typeof(T), registrations);
        }

        if (replaceExisting) registrations.Clear();
        if (registrations.Contains(decoder))
            throw new InvalidOperationException($"The decoder is already registered for {typeof(T).Name}.");

        registrations.Add(decoder);
        RemoveDecodedAssets(typeof(T));
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

    public T Get<T>(string id)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var asset = Get(id);
        var cacheKey = new DecodedAssetKey(asset.Id, typeof(T));
        if (_decodedAssets.TryGetValue(cacheKey, out var cached)) return (T)cached;

        if (!_decoders.TryGetValue(typeof(T), out var registrations))
            throw new InvalidOperationException($"No asset decoder is registered for {typeof(T).Name}.");

        var decoder = registrations
            .Cast<IAssetDecoder<T>>()
            .FirstOrDefault(candidate => candidate.CanDecode(asset));
        if (decoder is null)
            throw new InvalidDataException(
                $"None of the registered {typeof(T).Name} decoders accepts asset '{asset.Id}' with content type '{asset.ContentType ?? "unknown"}'.");

        var decoded = decoder.Decode(asset);
        if (decoded is null)
            throw new InvalidDataException($"The {typeof(T).Name} decoder returned null for asset '{asset.Id}'.");

        _decodedAssets.Add(cacheKey, decoded);
        return decoded;
    }

    public T GetJson<T>(string id)
    {
        return AssetProviderExtensions.GetJson<T>(this, id);
    }

    public ImageAsset GetImage(string id)
    {
        return Get<ImageAsset>(id);
    }

    public void Dispose()
    {
        if (_disposed) return;

        foreach (var source in _sources) source.Dispose();
        _sources.Clear();
        _decodedAssets.Clear();
        _decoders.Clear();
        _assets.Clear();
        _disposed = true;
    }

    private void AddSource(IAssetSource source)
    {
        _sources.Add(source);
        foreach (var asset in source.LoadAssets())
        {
            if (!_assets.TryAdd(asset.Id, asset))
                throw new InvalidDataException(
                    $"Asset id '{asset.Id}' from source '{source.Name}' is already provided by another source.");
        }
    }

    private void RemoveDecodedAssets(Type assetType)
    {
        foreach (var key in _decodedAssets.Keys.Where(key => key.AssetType == assetType).ToArray())
            _decodedAssets.Remove(key);
    }
}
