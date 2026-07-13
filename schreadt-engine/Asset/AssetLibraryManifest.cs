using Newtonsoft.Json;
using Schreadt_Engine.Core;

namespace Schreadt_Engine.Asset;

public class AssetLibraryManifest
{
    public static AssetLibraryManifest? ManifestFromName(string name)
    {
        var configString = FileHandler.ReadFileAsString(FileType.Manifest, name);
        if (configString is null) return null;
        var manifestData = JsonConvert.DeserializeObject<ManifestData>(configString);
        if (!IsValidManifestData(manifestData)) return null;

        return new AssetLibraryManifest(manifestData);
    }

    public static bool IsValidManifestData(ManifestData manifestData)
    {
        if (manifestData.Name is null) return false;
        if (manifestData.Version is null or < 1) return false;
        if (manifestData.Type is null or "") return false;
        return true;
    }

    private ManifestData _data;

    public string Name => _data.Name;
    public string Type => _data.Type;

    private AssetLibraryManifest(ManifestData manifestData)
    {
        _data = manifestData;
    }
}

public struct ManifestData
{
    [JsonProperty("name")]
    public string? Name { get; set; }

    [JsonProperty("version")]
    public int? Version { get; set; }

    [JsonProperty("type")]
    public string? Type { get; set; }
}