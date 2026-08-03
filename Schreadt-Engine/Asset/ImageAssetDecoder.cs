namespace Schreadt_Engine.Asset;

public sealed class ImageAssetDecoder : IAssetDecoder<ImageAsset>
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".bmp", ".gif", ".hdr", ".jpg", ".jpeg", ".pic", ".png", ".pnm", ".psd", ".tga"
    };

    public bool CanDecode(AssetRecord asset)
    {
        ArgumentNullException.ThrowIfNull(asset);

        var contentType = asset.ContentType?.Split(';', 2)[0].Trim();
        return contentType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true ||
               SupportedExtensions.Contains(Path.GetExtension(asset.SourcePath));
    }

    public ImageAsset Decode(AssetRecord asset)
    {
        return ImageAsset.Decode(asset);
    }
}
