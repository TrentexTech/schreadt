namespace Schreadt_Engine.Core;

/// <summary>
/// Owns a fixed-size RGBA pixel buffer whose version advances whenever new
/// content is published.
/// </summary>
public sealed class PixelSurface : IDisposable
{
    private byte[] _pixels;
    private bool _disposed;

    public int Width { get; }

    public int Height { get; }

    public long Version { get; private set; }

    public bool IsDisposed => _disposed;

    public ReadOnlyMemory<byte> Pixels
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _pixels;
        }
    }

    internal event Action<PixelSurface>? Disposed;

    public PixelSurface(int width, int height)
    {
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));

        int byteCount;
        try
        {
            byteCount = checked(width * height * 4);
        }
        catch (OverflowException)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Pixel surface dimensions are too large.");
        }

        Width = width;
        Height = height;
        _pixels = new byte[byteCount];
    }

    /// <summary>Copies and publishes a complete RGBA image.</summary>
    public void Update(ReadOnlySpan<byte> rgbaPixels)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (rgbaPixels.Length != _pixels.Length)
        {
            throw new ArgumentException(
                $"The pixel buffer must contain exactly {_pixels.Length} RGBA bytes.",
                nameof(rgbaPixels));
        }

        var nextVersion = checked(Version + 1);
        rgbaPixels.CopyTo(_pixels);
        Version = nextVersion;
    }

    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        _pixels = [];
        try
        {
            Disposed?.Invoke(this);
        }
        finally
        {
            Disposed = null;
        }
    }
}

internal sealed class PixelSurfaceUploadState
{
    internal PixelSurface? Surface { get; private set; }

    internal long Version { get; private set; } = -1;

    internal bool RequiresUpload(PixelSurface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);
        return !ReferenceEquals(Surface, surface) || Version != surface.Version;
    }

    internal void MarkUploaded(PixelSurface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);
        Surface = surface;
        Version = surface.Version;
    }

    internal bool Forget(PixelSurface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);
        if (!ReferenceEquals(Surface, surface)) return false;

        Clear();
        return true;
    }

    internal void Clear()
    {
        Surface = null;
        Version = -1;
    }
}
