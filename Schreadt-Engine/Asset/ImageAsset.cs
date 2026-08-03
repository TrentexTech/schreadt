using StbImageSharp;

namespace Schreadt_Engine.Asset;

public sealed class ImageAsset
{
    private readonly byte[] _pixels;

    public string Id { get; }
    public int Width { get; }
    public int Height { get; }
    public ReadOnlyMemory<byte> Pixels => _pixels;

    private ImageAsset(string id, int width, int height, byte[] pixels)
    {
        Id = id;
        Width = width;
        Height = height;
        _pixels = pixels;
    }

    internal static ImageAsset Decode(AssetRecord asset)
    {
        ArgumentNullException.ThrowIfNull(asset);

        try
        {
            var decoded = ImageResult.FromMemory(asset.Data.ToArray(), ColorComponents.RedGreenBlueAlpha);
            if (decoded.Width <= 0 || decoded.Height <= 0 || decoded.Data.Length != decoded.Width * decoded.Height * 4)
                throw new InvalidDataException($"Image asset '{asset.Id}' decoded to invalid RGBA pixel data.");

            return new ImageAsset(asset.Id, decoded.Width, decoded.Height, decoded.Data);
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new InvalidDataException($"Asset '{asset.Id}' could not be decoded as an image.", exception);
        }
    }
}
