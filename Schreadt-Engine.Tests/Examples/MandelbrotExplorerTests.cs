using Mandelbrot_Explorer.Logic;
using Schreadt_Engine.Core;
using Silk.NET.Maths;

namespace Schreadt_Engine.Tests.Examples;

public sealed class MandelbrotExplorerTests
{
    [Theory]
    [InlineData(0.0, 0.0)]
    [InlineData(-1.0, 0.0)]
    [InlineData(-0.125, 0.744)]
    public void EscapeIterations_LeavesInteriorPointsBounded(double real, double imaginary)
    {
        const int maximum = 128;

        var iterations = MandelbrotGenerator.EscapeIterations(real, imaginary, maximum, out _);

        Assert.Equal(maximum, iterations);
    }

    [Fact]
    public void EscapeIterations_RecognizesEscapingPoint()
    {
        var iterations = MandelbrotGenerator.EscapeIterations(1.0, 1.0, 128, out var magnitudeSquared);

        Assert.InRange(iterations, 1, 127);
        Assert.True(magnitudeSquared > 4.0);
    }

    [Fact]
    public void Render_IsSymmetricAcrossTheRealAxis()
    {
        var pixels = MandelbrotGenerator.Render(
            new MandelbrotView(-0.5, 0.0, 3.5, 64),
            31,
            20,
            0,
            TestContext.Current.CancellationToken);
        const int rowLength = 31 * 4;

        for (var row = 0; row < 10; row++)
        {
            var top = pixels.AsSpan(row * rowLength, rowLength);
            var bottom = pixels.AsSpan((19 - row) * rowLength, rowLength);
            Assert.True(top.SequenceEqual(bottom));
        }
    }

    [Fact]
    public void ZoomAt_KeepsThePointUnderTheCursorFixed()
    {
        var view = new MandelbrotView(-0.5, 0.0, 3.5, 128);
        var cursor = new Vector2D<double>(0.72, 0.36);
        var target = view.ComplexPointAt(cursor, 16.0 / 9.0);

        var zoomed = view.ZoomAt(cursor, 0.4, 16.0 / 9.0);

        var zoomedTarget = zoomed.ComplexPointAt(cursor, 16.0 / 9.0);
        Assert.Equal(target.X, zoomedTarget.X, 12);
        Assert.Equal(target.Y, zoomedTarget.Y, 12);
    }

    [Fact]
    public void Canvas_CancelsAndDiscardsStaleGenerationBeforePublishingLatestPixels()
    {
        var requests = new List<RenderRequest>();
        var canvas = CreateControlledCanvas(requests);
        canvas.Init();
        var initialSurface = canvas.Surface;

        canvas.Pan(0.1, 0.0);

        Assert.True(requests[0].CancellationToken.IsCancellationRequested);
        requests[0].Completion.SetResult(FilledPixels(11));
        canvas.Update(0.0);
        Assert.Equal(0, initialSurface.Version);

        requests[1].Completion.SetResult(FilledPixels(22));
        canvas.Update(0.0);

        Assert.Equal(1, initialSurface.Version);
        Assert.All(initialSurface.Pixels.ToArray(), value => Assert.Equal(22, value));
        canvas.Shutdown();
    }

    [Fact]
    public void Canvas_RepeatedDrawsUploadOnlyWhenPublishedVersionChanges()
    {
        var requests = new List<RenderRequest>();
        var canvas = CreateControlledCanvas(requests);
        var renderer = new RecordingPixelRenderContext();
        canvas.Init();
        requests[0].Completion.SetResult(FilledPixels(7));
        canvas.Update(0.0);

        canvas.Render(renderer);
        canvas.Render(renderer);

        Assert.Equal(2, renderer.DrawCount);
        Assert.Equal(1, renderer.UploadCount);

        canvas.Pan(0.1, 0.0);
        canvas.Render(renderer);
        Assert.Equal(1, renderer.UploadCount);

        requests[1].Completion.SetResult(FilledPixels(9));
        canvas.Update(0.0);
        canvas.Render(renderer);

        Assert.Equal(2, renderer.UploadCount);
        canvas.Shutdown();
    }

    [Fact]
    public void Canvas_ShutdownCancelsActiveGenerationAndDisposesSurface()
    {
        var requests = new List<RenderRequest>();
        var canvas = CreateControlledCanvas(requests);
        canvas.Init();
        var surface = canvas.Surface;

        canvas.Shutdown();

        Assert.True(requests[0].CancellationToken.IsCancellationRequested);
        Assert.True(surface.IsDisposed);
        requests[0].Completion.TrySetCanceled(requests[0].CancellationToken);
    }

    [Fact]
    public void Render_HonorsAnAlreadyCanceledRequest()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        cancellation.Cancel();

        Assert.ThrowsAny<OperationCanceledException>(() => MandelbrotGenerator.Render(
            new MandelbrotView(-0.5, 0.0, 3.5, 64),
            31,
            20,
            0,
            cancellation.Token));
    }

    [Fact]
    public async Task RenderAsync_MatchesSynchronousGeneration()
    {
        var view = new MandelbrotView(-0.743, 0.131, 0.01, 64);

        var expected = MandelbrotGenerator.Render(
            view,
            24,
            16,
            2,
            TestContext.Current.CancellationToken);
        var actual = await MandelbrotGenerator.RenderAsync(
            view,
            24,
            16,
            2,
            TestContext.Current.CancellationToken);

        Assert.Equal(expected, actual);
    }

    private static MandelbrotCanvas CreateControlledCanvas(List<RenderRequest> requests)
    {
        return new MandelbrotCanvas(2, 2, (_, _, _, _, cancellationToken) =>
        {
            var completion = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            requests.Add(new RenderRequest(cancellationToken, completion));
            return completion.Task;
        });
    }

    private static byte[] FilledPixels(byte value) => Enumerable.Repeat(value, 16).ToArray();

    private sealed record RenderRequest(
        CancellationToken CancellationToken,
        TaskCompletionSource<byte[]> Completion);

    private sealed class RecordingPixelRenderContext : IPixelRenderContext2D
    {
        private readonly PixelSurfaceUploadState _uploads = new();

        internal int DrawCount { get; private set; }

        internal int UploadCount { get; private set; }

        public Vector2D<int> ViewportSize { get; } = new(2, 2);

        public void DrawScreenPixels(PixelSurface surface, TextureSampling sampling = TextureSampling.Nearest)
        {
            DrawCount++;
            if (!_uploads.RequiresUpload(surface)) return;

            _uploads.MarkUploaded(surface);
            UploadCount++;
        }

        public void DrawCircle(Vector2D<double> center, double radius, Vector4D<float> color)
        {
        }

        public void DrawRectangle(
            Vector2D<double> center,
            Vector2D<double> size,
            Vector4D<float> color,
            double rotationRadians = 0.0)
        {
        }

        public void DrawPolygon(
            Vector2D<double> center,
            IReadOnlyList<Vector2D<double>> localVertices,
            Vector2D<double> scale,
            double rotationRadians,
            Vector4D<float> color)
        {
        }

        public void DrawSprite(
            string imageAssetId,
            Vector2D<double> center,
            Vector2D<double> size,
            Vector4D<float> tint,
            double rotationRadians = 0.0,
            TextureRegion? region = null,
            TextureSampling sampling = TextureSampling.Linear)
        {
        }

        public void DrawText(
            string text,
            Vector2D<float> position,
            float scale,
            Vector4D<float> color,
            Vector4D<float> backgroundColor,
            float padding = 0.0f)
        {
        }

        public void DrawScreenRectangle(
            Vector2D<float> position,
            Vector2D<float> size,
            Vector4D<float> color)
        {
        }
    }
}
