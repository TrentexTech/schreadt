using Schreadt_Engine.Component;
using Schreadt_Engine.Core;
using Schreadt_Engine.Gui;
using Silk.NET.Maths;

namespace Schreadt_Engine.Tests.Gui;

public sealed class InteractiveGuiTests
{
    [Fact]
    public void Button_ClicksAfterPressAndReleaseInsideBounds()
    {
        using var input = new InputManager();
        var gui = new GuiSystem();
        var button = gui.AddButton("PLAY");
        button.Position = new Vector2D<float>(10.0f, 20.0f);
        var clickCount = 0;
        button.Clicked += (_, _) => clickCount++;
        gui.Render(new TestRenderContext());

        input.ProcessMouseMove(new System.Numerics.Vector2(12.0f, 22.0f));
        gui.Update(input);
        input.ProcessMouseDown(InputMouseButton.Left);
        gui.Update(input);

        Assert.True(button.IsHovered);
        Assert.True(button.IsPressed);
        Assert.True(gui.IsPointerCaptured);

        input.EndFrame();
        input.ProcessMouseUp(InputMouseButton.Left);
        gui.Update(input);

        Assert.Equal(1, clickCount);
        Assert.False(button.IsPressed);
        Assert.False(gui.IsPointerCaptured);
    }

    [Fact]
    public void Button_DoesNotClickWhenPointerIsReleasedOutsideBounds()
    {
        using var input = new InputManager();
        var gui = new GuiSystem();
        var button = gui.AddButton("PLAY");
        var clickCount = 0;
        button.Clicked += (_, _) => clickCount++;
        gui.Render(new TestRenderContext());

        input.ProcessMouseMove(new System.Numerics.Vector2(2.0f, 2.0f));
        input.ProcessMouseDown(InputMouseButton.Left);
        gui.Update(input);
        input.EndFrame();
        input.ProcessMouseMove(new System.Numerics.Vector2(500.0f, 500.0f));
        input.ProcessMouseUp(InputMouseButton.Left);
        gui.Update(input);

        Assert.Equal(0, clickCount);
        Assert.False(button.IsHovered);
        Assert.False(button.IsPressed);
    }

    [Fact]
    public void GuiSystem_RoutesPointerToLastAddedOverlappingControl()
    {
        using var input = new InputManager();
        var gui = new GuiSystem();
        var bottomButton = gui.AddButton("BOTTOM");
        var topButton = gui.AddButton("TOP");
        var bottomClicks = 0;
        var topClicks = 0;
        bottomButton.Clicked += (_, _) => bottomClicks++;
        topButton.Clicked += (_, _) => topClicks++;
        gui.Render(new TestRenderContext());

        input.ProcessMouseMove(new System.Numerics.Vector2(2.0f, 2.0f));
        input.ProcessMouseDown(InputMouseButton.Left);
        input.ProcessMouseUp(InputMouseButton.Left);
        gui.Update(input);

        Assert.Equal(0, bottomClicks);
        Assert.Equal(1, topClicks);
        Assert.False(bottomButton.IsHovered);
        Assert.True(topButton.IsHovered);
    }

    private sealed class TestRenderContext : IRenderContext2D
    {
        public Vector2D<int> ViewportSize => new(800, 600);

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
