using Schreadt_Engine.Core;
using Schreadt_Engine.Gui;
using Silk.NET.Maths;

namespace Schreadt_Engine.Tests.Gui;

public sealed class PerformanceOverlayTests
{
    [Theory]
    [InlineData(1280)]
    [InlineData(1920)]
    public void Render_AnchorsPanelToTopRight(int viewportWidth)
    {
        var gui = new GuiSystem();
        _ = new PerformanceOverlay(gui);
        var renderer = new RecordingRenderContext(viewportWidth, 720);

        gui.Render(renderer);

        var panel = Assert.Single(renderer.ScreenRectangles);
        Assert.Equal(12.0f, panel.Position.Y);
        Assert.Equal(viewportWidth - 12.0f, panel.Position.X + panel.Size.X, 3);
    }

    private sealed class RecordingRenderContext(int width, int height) : IRenderContext2D
    {
        internal List<(Vector2D<float> Position, Vector2D<float> Size)> ScreenRectangles { get; } = [];

        public Vector2D<int> ViewportSize { get; } = new(width, height);

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
            ScreenRectangles.Add((position, size));
        }
    }
}
