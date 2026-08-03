using Schreadt_Engine.Component;
using Schreadt_Engine.Core;
using Schreadt_Engine.Gui;
using Silk.NET.Maths;

namespace Schreadt_Engine.Tests.Gui;

public sealed class GuiScalingTests
{
    [Fact]
    public void Render_ScalesGuiDrawingFromReferenceHeight()
    {
        var gui = new GuiSystem(720.0f);
        var label = gui.AddLabel("TEST");
        label.Position = new Vector2D<float>(10.0f, 20.0f);
        label.Scale = 2.0f;
        label.Padding = 3.0f;
        var context = new RecordingRenderContext(2560, 1440);

        gui.Render(context);

        Assert.Equal(new Vector2D<float>(20.0f, 40.0f), context.TextPosition);
        Assert.Equal(4.0f, context.TextScale);
        Assert.Equal(6.0f, context.TextPadding);
        Assert.Equal(new Vector2D<float>(10.0f, 20.0f), label.Bounds.Position);
    }

    [Fact]
    public void Update_MapsPointerBackIntoScaledGuiCoordinates()
    {
        using var input = new InputManager();
        var gui = new GuiSystem(720.0f);
        var button = gui.AddButton("PLAY");
        button.Position = new Vector2D<float>(100.0f, 100.0f);
        var clicks = 0;
        button.Clicked += (_, _) => clicks++;
        gui.Render(new RecordingRenderContext(2560, 1440));

        input.ProcessMouseMove(new System.Numerics.Vector2(220.0f, 220.0f));
        input.ProcessMouseDown(InputMouseButton.Left);
        input.ProcessMouseUp(InputMouseButton.Left);
        gui.Update(input);

        Assert.Equal(1, clicks);
        Assert.True(button.IsHovered);
    }

    [Fact]
    public void Update_AccountsForLetterboxViewportOffset()
    {
        using var input = new InputManager();
        var gui = new GuiSystem(720.0f);
        var button = gui.AddButton("PLAY");
        button.Position = new Vector2D<float>(100.0f, 100.0f);
        var clicks = 0;
        button.Clicked += (_, _) => clicks++;
        gui.SetViewportSizes(
            new Vector2D<int>(1920, 720),
            new Vector2D<int>(1920, 720),
            new Vector2D<int>(320, 0),
            new Vector2D<int>(1280, 720));
        gui.Render(new RecordingRenderContext(1280, 720));

        input.ProcessMouseMove(new System.Numerics.Vector2(430.0f, 110.0f));
        input.ProcessMouseDown(InputMouseButton.Left);
        input.ProcessMouseUp(InputMouseButton.Left);
        gui.Update(input);

        Assert.Equal(1, clicks);
    }

    private sealed class RecordingRenderContext(int width, int height) : IRenderContext2D
    {
        public Vector2D<int> ViewportSize { get; } = new(width, height);
        public Vector2D<float> TextPosition { get; private set; }
        public float TextScale { get; private set; }
        public float TextPadding { get; private set; }

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
            TextPosition = position;
            TextScale = scale;
            TextPadding = padding;
        }

        public void DrawScreenRectangle(
            Vector2D<float> position,
            Vector2D<float> size,
            Vector4D<float> color)
        {
        }
    }
}
