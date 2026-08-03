namespace Schreadt_Engine.Asset;

public static class AssetProviderExtensions
{
    public static T GetJson<T>(this IAssetProvider provider, string id)
    {
        ArgumentNullException.ThrowIfNull(provider);
        var asset = provider.Get(id);
        return new JsonAssetDecoder<T>().Decode(asset);
    }

    public static ImageAsset GetImage(this IAssetProvider provider, string id)
    {
        ArgumentNullException.ThrowIfNull(provider);
        return provider.Get<ImageAsset>(id);
    }
}
