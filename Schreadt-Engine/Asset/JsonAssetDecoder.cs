using Newtonsoft.Json;

namespace Schreadt_Engine.Asset;

public sealed class JsonAssetDecoder<T> : IAssetDecoder<T>
{
    public bool CanDecode(AssetRecord asset)
    {
        ArgumentNullException.ThrowIfNull(asset);

        var contentType = asset.ContentType?.Split(';', 2)[0].Trim();
        return string.Equals(contentType, "application/json", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(Path.GetExtension(asset.SourcePath), ".json", StringComparison.OrdinalIgnoreCase);
    }

    public T Decode(AssetRecord asset)
    {
        ArgumentNullException.ThrowIfNull(asset);

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
}
