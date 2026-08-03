namespace Schreadt_Engine.Asset;

public interface IAssetProvider
{
    int Count { get; }
    IReadOnlyCollection<string> Ids { get; }

    bool Contains(string id);
    AssetRecord Get(string id);
    ReadOnlyMemory<byte> GetBytes(string id);
    string GetText(string id);
    T Get<T>(string id);
}

public interface IAssetSource : IDisposable
{
    string Name { get; }
    IReadOnlyCollection<AssetRecord> LoadAssets();
}

public interface IAssetDecoder<out T>
{
    bool CanDecode(AssetRecord asset);
    T Decode(AssetRecord asset);
}
