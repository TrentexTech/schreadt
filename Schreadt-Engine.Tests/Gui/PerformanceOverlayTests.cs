using System.Numerics;
using Schreadt_Engine.Core;
using Schreadt_Engine.Collision;
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

    [Fact]
    public void Update_ReportsRenderingSimulationPhysicsAndMemoryMetrics()
    {
        var gui = new GuiSystem();
        var overlay = new PerformanceOverlay(gui);
        var runtime = new RuntimeController();
        var renderer = new RecordingRenderContext(1280, 720);
        var display = PerformanceDisplayMetrics.Create(
            new Vector2D<int>(1280, 720),
            WindowDisplayState.Maximized,
            vSync: false,
            new Vector2D<int>(2560, 1440),
            new Vector2D<int>(320, 180),
            new Vector2D<int>(1920, 1080),
            gui);

        overlay.Update(
            1.0 / 60.0,
            runtime,
            fixedStepCount: 2,
            new CollisionStatistics2D(10, 8, 3, 28, 12, 2, 3, 3, 8, 0.42, 4, 3),
            new RenderStatistics(14, 92, 276, 1, 1280L * 720 * 4),
            new FrameCompositionStatistics(
                2.4,
                0.2,
                0.1,
                1.2,
                0.3,
                0.1,
                0.2,
                0.3,
                [
                    new FrameCompositionPassTiming("Lightning", FrameCompositionStage.BeforeScene, 0.06),
                    new FrameCompositionPassTiming("Rain", FrameCompositionStage.AfterScene, 0.22),
                    new FrameCompositionPassTiming("Screen Flash", FrameCompositionStage.BeforeGui, 0.14)
                ]),
            display);
        gui.Render(renderer);

        var text = string.Join('\n', renderer.TextDraws.Select(draw => draw.Text));
        Assert.Contains("PERFORMANCE OVERLAY  [F3: HIDE]", text);
        Assert.Contains("FPS: 60.0", text);
        Assert.Contains("TIME: 16.67 MS", text);
        Assert.Contains("DRAW: 14  PRIM: 92  VERT: 276", text);
        Assert.Contains("UPLOAD: 1  DATA: 3.52 MB", text);
        Assert.Contains("TOTAL: 2.40 MS", text);
        Assert.Contains("CORE: BG 0.20  SCENE 1.20  DIAG 0.10  GUI 0.30", text);
        Assert.Contains("STAGES: PRE 0.10  POST 0.30  PREGUI 0.20", text);
        Assert.Contains("PASS PRE LIGHTNING: 0.06 MS", text);
        Assert.Contains("PASS POST RAIN: 0.22 MS", text);
        Assert.Contains("PASS PREGUI SCREEN FLASH: 0.14 MS", text);
        Assert.Contains("SIM: RUNNING  FIXED: 2  SCALE: 1.00", text);
        Assert.Contains("PHYS: 8/10  JOINT: 3/4", text);
        Assert.Contains("CONTACT: 2  POINTS: 3", text);
        Assert.Contains("CHECKS: 28 PAIR  12 NARROW", text);
        Assert.Contains("SOLVER: 8V/3P  0.42 MS", text);
        Assert.Contains("WINDOW: 1280X720  MAXIMIZED  VSYNC: OFF", text);
        Assert.Contains("FRAMEBUFFER: 2560X1440  SCALE: 2.00X/2.00X", text);
        Assert.Contains("VIEWPORT: 1920X1080  OFFSET: 320,180", text);
        Assert.Contains("LOGICAL: 1280.0X720.0  SCALE: 1.50X  REF: 720.0", text);
        Assert.Contains("MEM:", text);
        Assert.Contains("GC:", text);

        var unsupportedCharacters = text
            .Where(character => character is not '\r' and not '\n' && !BitmapFont5x7.Supports(character))
            .Distinct()
            .ToArray();
        Assert.True(
            unsupportedCharacters.Length == 0,
            $"The performance overlay contains unsupported bitmap-font characters: {string.Join(' ', unsupportedCharacters)}");

        string[] sections =
        [
            "[FRAME]",
            "[RENDERING]",
            "[COMPOSITION]",
            "[SIMULATION]",
            "[PHYSICS]",
            "[DISPLAY]",
            "[GUI]",
            "[MEMORY]"
        ];
        var previousSectionIndex = -1;
        foreach (var section in sections)
        {
            var sectionIndex = text.IndexOf(section, StringComparison.Ordinal);
            Assert.True(sectionIndex > previousSectionIndex, $"Section {section} is missing or out of order.");
            previousSectionIndex = sectionIndex;
        }

        var sectionColors = renderer.TextDraws
            .Where(draw => sections.Contains(draw.Text, StringComparer.Ordinal))
            .Select(draw => draw.Color)
            .ToArray();
        Assert.Equal(sections.Length, sectionColors.Length);
        Assert.Equal(sections.Length, sectionColors.Distinct().Count());

        Assert.All(renderer.TextDraws, draw => Assert.Equal(1.75f, draw.Scale));
        var frameHeading = Assert.Single(renderer.TextDraws, draw => draw.Text == "[FRAME]");
        var frameValues = Assert.Single(renderer.TextDraws, draw => draw.Text.StartsWith("FPS:", StringComparison.Ordinal));
        var headingHeight = (BitmapFont5x7.LineAdvance - 1) * frameHeading.Scale;
        var headingToValuesGap = frameValues.Position.Y - frameHeading.Position.Y - headingHeight;
        Assert.Equal(frameHeading.Scale, headingToValuesGap, 3);
    }

    [Fact]
    public void HandleInput_F3TogglesOverlayVisibility()
    {
        var gui = new GuiSystem();
        var overlay = new PerformanceOverlay(gui);

        Assert.True(overlay.IsVisible);

        overlay.HandleInput(new TestInputState(InputKey.F3));
        Assert.False(overlay.IsVisible);
        var hiddenRenderer = new RecordingRenderContext(1280, 720);
        gui.Render(hiddenRenderer);
        Assert.Empty(hiddenRenderer.TextDraws);
        Assert.Empty(hiddenRenderer.ScreenRectangles);

        overlay.HandleInput(new TestInputState(InputKey.F3));
        Assert.True(overlay.IsVisible);
        var visibleRenderer = new RecordingRenderContext(1280, 720);
        gui.Render(visibleRenderer);
        Assert.NotEmpty(visibleRenderer.TextDraws);
        Assert.Single(visibleRenderer.ScreenRectangles);
    }

    private sealed class RecordingRenderContext(int width, int height) : IRenderContext2D
    {
        internal List<(Vector2D<float> Position, Vector2D<float> Size)> ScreenRectangles { get; } = [];
        internal List<(string Text, Vector2D<float> Position, float Scale, Vector4D<float> Color)> TextDraws { get; } = [];

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
            TextDraws.Add((text, position, scale, color));
        }

        public void DrawScreenRectangle(
            Vector2D<float> position,
            Vector2D<float> size,
            Vector4D<float> color)
        {
            ScreenRectangles.Add((position, size));
        }
    }

    private sealed class TestInputState(InputKey pressedKey) : IInputState
    {
        public bool Available => true;
        public Vector2 MousePosition => default;
        public Vector2 MouseDelta => default;
        public Vector2 ScrollDelta => default;
        public Vector2D<double> MouseViewportPosition => default;
        public double ViewportAspectRatio => 16.0 / 9.0;
        public string TextInput => string.Empty;

        public event Action<InputKey>? KeyPressed { add { } remove { } }
        public event Action<InputKey>? KeyReleased { add { } remove { } }
        public event Action<char>? CharacterTyped { add { } remove { } }
        public event Action<InputMouseButton>? MouseButtonPressed { add { } remove { } }
        public event Action<InputMouseButton>? MouseButtonReleased { add { } remove { } }
        public event Action<Vector2>? MouseMoved { add { } remove { } }
        public event Action<Vector2>? Scrolled { add { } remove { } }

        public bool IsKeyDown(InputKey key) => false;
        public bool WasKeyPressed(InputKey key) => key == pressedKey;
        public bool WasKeyReleased(InputKey key) => false;
        public bool IsMouseButtonDown(InputMouseButton button) => false;
        public bool WasMouseButtonPressed(InputMouseButton button) => false;
        public bool WasMouseButtonReleased(InputMouseButton button) => false;
        public bool IsActionDown(string action) => false;
        public bool WasActionPressed(string action) => false;
        public bool WasActionReleased(string action) => false;
    }
}
