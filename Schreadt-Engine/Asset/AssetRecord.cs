using System.Text;

namespace Schreadt_Engine.Asset;

public sealed class AssetRecord
{
    private static readonly Encoding Utf8 = new UTF8Encoding(false, true);
    private readonly byte[] _data;
    private string? _text;

    public string Id { get; }
    public string? ContentType { get; }
    public string SourcePath { get; }
    public ReadOnlyMemory<byte> Data => _data;

    public AssetRecord(string id, string? contentType, string sourcePath, ReadOnlySpan<byte> data)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

        Id = AssetId.Normalize(id);
        ContentType = string.IsNullOrWhiteSpace(contentType) ? null : contentType.Trim();
        SourcePath = Path.GetFullPath(sourcePath);
        _data = data.ToArray();
    }

    public string GetText()
    {
        return _text ??= Utf8.GetString(_data);
    }
}
