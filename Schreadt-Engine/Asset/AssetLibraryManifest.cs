using Newtonsoft.Json;

namespace Schreadt_Engine.Asset;

public sealed class AssetLibraryManifest
{
    internal string ManifestPath { get; }

    public string Name { get; }
    public int Version { get; }
    public string Type { get; }
    public string Root { get; }
    public IReadOnlyList<AssetManifestEntry> Assets { get; }

    private AssetLibraryManifest(
        string manifestPath,
        string name,
        int version,
        string type,
        string root,
        IReadOnlyList<AssetManifestEntry> assets)
    {
        ManifestPath = manifestPath;
        Name = name;
        Version = version;
        Type = type;
        Root = root;
        Assets = assets;
    }

    internal static AssetLibraryManifest Load(string manifestPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestPath);

        if (!File.Exists(manifestPath))
            throw new FileNotFoundException($"Asset library manifest was not found: '{manifestPath}'.", manifestPath);

        ManifestData? data;
        try
        {
            data = JsonConvert.DeserializeObject<ManifestData>(File.ReadAllText(manifestPath));
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"Asset library manifest '{manifestPath}' contains invalid JSON.", exception);
        }

        if (data is null) throw new InvalidDataException($"Asset library manifest '{manifestPath}' is empty.");
        if (string.IsNullOrWhiteSpace(data.Name))
            throw new InvalidDataException($"Asset library manifest '{manifestPath}' must define a name.");
        if (data.Version < 1)
            throw new InvalidDataException($"Asset library manifest '{manifestPath}' must use a version of at least 1.");
        if (string.IsNullOrWhiteSpace(data.Type))
            throw new InvalidDataException($"Asset library manifest '{manifestPath}' must define a library type.");

        var entries = new List<AssetManifestEntry>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in data.Assets ?? [])
        {
            if (string.IsNullOrWhiteSpace(entry.Id) || string.IsNullOrWhiteSpace(entry.Path))
                throw new InvalidDataException($"Every asset in manifest '{manifestPath}' must define an id and path.");

            var id = AssetId.Normalize(entry.Id);
            if (!ids.Add(id))
                throw new InvalidDataException($"Manifest '{manifestPath}' defines asset id '{id}' more than once.");

            entries.Add(new AssetManifestEntry(id, entry.Path, entry.ContentType));
        }

        return new AssetLibraryManifest(
            Path.GetFullPath(manifestPath),
            data.Name.Trim(),
            data.Version,
            data.Type.Trim(),
            string.IsNullOrWhiteSpace(data.Root) ? "." : data.Root,
            entries.AsReadOnly());
    }

    private sealed class ManifestData
    {
        [JsonProperty("name")]
        public string? Name { get; init; }

        [JsonProperty("version")]
        public int Version { get; init; }

        [JsonProperty("type")]
        public string? Type { get; init; }

        [JsonProperty("root")]
        public string? Root { get; init; }

        [JsonProperty("assets")]
        public AssetEntryData[]? Assets { get; init; }
    }

    private sealed class AssetEntryData
    {
        [JsonProperty("id")]
        public string? Id { get; init; }

        [JsonProperty("path")]
        public string? Path { get; init; }

        [JsonProperty("contentType")]
        public string? ContentType { get; init; }
    }
}

public sealed record AssetManifestEntry(string Id, string Path, string? ContentType);
