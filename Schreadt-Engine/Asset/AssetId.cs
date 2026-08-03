namespace Schreadt_Engine.Asset;

internal static class AssetId
{
    internal static string Normalize(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var normalized = id.Trim().Replace('\\', '/');
        var segments = normalized.Split('/');
        if (normalized.StartsWith('/') || segments.Any(segment => segment is "" or "." or ".."))
            throw new ArgumentException($"Asset id '{id}' is not a valid relative identifier.", nameof(id));

        return string.Join('/', segments);
    }
}
