using Schreadt_Engine.Core;
using Silk.NET.Maths;

namespace Schreadt_Engine.Tests.Core;

public sealed class RendererViewportTests
{
    [Fact]
    public void CalculateViewport_AddsPillarboxesWhenFramebufferIsTooWide()
    {
        var viewport = Renderer.CalculateViewport(1920, 720, 16.0 / 9.0);

        Assert.Equal(new Vector2D<int>(320, 0), viewport.Offset);
        Assert.Equal(new Vector2D<int>(1280, 720), viewport.Size);
    }

    [Fact]
    public void CalculateViewport_AddsLetterboxesWhenFramebufferIsTooTall()
    {
        var viewport = Renderer.CalculateViewport(1280, 1000, 16.0 / 9.0);

        Assert.Equal(new Vector2D<int>(0, 140), viewport.Offset);
        Assert.Equal(new Vector2D<int>(1280, 720), viewport.Size);
    }

    [Fact]
    public void CalculateViewport_UsesWholeFramebufferAtTargetAspectRatio()
    {
        var viewport = Renderer.CalculateViewport(2560, 1440, 16.0 / 9.0);

        Assert.Equal(Vector2D<int>.Zero, viewport.Offset);
        Assert.Equal(new Vector2D<int>(2560, 1440), viewport.Size);
    }

    [Fact]
    public void InputManager_NormalizesMouseWithinConstrainedViewport()
    {
        using var input = new InputManager();
        input.SetViewportSizes(
            new Vector2D<int>(1920, 720),
            new Vector2D<int>(1920, 720),
            new Vector2D<int>(320, 0),
            new Vector2D<int>(1280, 720));

        input.ProcessMouseMove(new System.Numerics.Vector2(960.0f, 360.0f));

        Assert.Equal(new Vector2D<double>(0.5, 0.5), input.MouseViewportPosition);
        Assert.Equal(16.0 / 9.0, input.ViewportAspectRatio, 6);
    }
}
