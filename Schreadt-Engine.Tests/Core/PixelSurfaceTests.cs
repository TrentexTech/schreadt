using Schreadt_Engine.Core;

namespace Schreadt_Engine.Tests.Core;

public sealed class PixelSurfaceTests
{
    [Fact]
    public void Update_CopiesPixelsAndAdvancesVersion()
    {
        using var surface = new PixelSurface(2, 2);
        var source = Enumerable.Repeat((byte)17, 16).ToArray();

        surface.Update(source);
        source[0] = 99;

        Assert.Equal(1, surface.Version);
        Assert.Equal(17, surface.Pixels.Span[0]);
    }

    [Fact]
    public void UploadState_AdvancesOnlyAfterSuccessfulUploadIsRecorded()
    {
        using var surface = new PixelSurface(2, 2);
        var uploads = new PixelSurfaceUploadState();

        Assert.True(uploads.RequiresUpload(surface));
        Assert.True(uploads.RequiresUpload(surface));

        uploads.MarkUploaded(surface);

        Assert.False(uploads.RequiresUpload(surface));
        surface.Update(new byte[16]);
        Assert.True(uploads.RequiresUpload(surface));
    }

    [Fact]
    public void Dispose_NotifiesRetainersAndReleasesPixelMemory()
    {
        var surface = new PixelSurface(2, 2);
        PixelSurface? disposedSurface = null;
        surface.Disposed += disposed => disposedSurface = disposed;

        surface.Dispose();

        Assert.Same(surface, disposedSurface);
        Assert.True(surface.IsDisposed);
        Assert.Throws<ObjectDisposedException>(() => _ = surface.Pixels);
    }
}
