using Schreadt_Engine.Component;
using Schreadt_Engine.Gui;
using Silk.NET.Maths;

namespace Schreadt_Engine.Core;

/// <summary>Work submitted during the most recently completed rendered frame.</summary>
public readonly record struct RenderStatistics(
    int DrawCallCount,
    int PrimitiveCount,
    int VertexCount,
    int TextureUploadCount = 0,
    long TextureUploadByteCount = 0);

/// <summary>A world-space line segment used by batched background drawing.</summary>
public readonly record struct LineSegment2D(Vector2D<double> Start, Vector2D<double> End);

/// <summary>Describes the parallax-adjusted camera view used to draw a background.</summary>
public readonly record struct BackgroundView2D(
    Vector2D<double> Center,
    double RotationRadians,
    double OrthographicSize,
    double AspectRatio,
    Vector2D<double> VisibleMinimum,
    Vector2D<double> VisibleMaximum);

/// <summary>
/// Receives backend-independent two-dimensional draw commands.
/// </summary>
public interface IRenderContext2D
{
    Vector2D<int> ViewportSize { get; }

    void DrawCircle(Vector2D<double> center, double radius, Vector4D<float> color);

    void DrawRectangle(
        Vector2D<double> center,
        Vector2D<double> size,
        Vector4D<float> color,
        double rotationRadians = 0.0);

    void DrawPolygon(
        Vector2D<double> center,
        IReadOnlyList<Vector2D<double>> localVertices,
        Vector2D<double> scale,
        double rotationRadians,
        Vector4D<float> color);

    void DrawSprite(
        string imageAssetId,
        Vector2D<double> center,
        Vector2D<double> size,
        Vector4D<float> tint,
        double rotationRadians = 0.0,
        TextureRegion? region = null,
        TextureSampling sampling = TextureSampling.Linear);

    void DrawText(
        string text,
        Vector2D<float> position,
        float scale,
        Vector4D<float> color,
        Vector4D<float> backgroundColor,
        float padding = 0.0f);

    void DrawScreenRectangle(
        Vector2D<float> position,
        Vector2D<float> size,
        Vector4D<float> color);
}

/// <summary>
/// Supplies world drawing operations and the active view to scene backgrounds.
/// </summary>
public interface IBackgroundRenderContext2D : IRenderContext2D
{
    BackgroundView2D View { get; }

    /// <summary>Draws equally colored line segments in one batch.</summary>
    void DrawLines(IReadOnlyList<LineSegment2D> lines, Vector4D<float> color);
}

/// <summary>
/// Extends two-dimensional rendering with a viewport-sized RGBA pixel surface.
/// Pixel rows are ordered from top to bottom and each pixel contains four bytes
/// in red, green, blue, alpha order. A surface is uploaded only when its version
/// advances.
/// </summary>
public interface IPixelRenderContext2D : IRenderContext2D
{
    void DrawScreenPixels(
        PixelSurface surface,
        TextureSampling sampling = TextureSampling.Nearest);
}

/// <summary>
/// Owns complete camera-based frames in addition to accepting draw commands.
/// </summary>
public interface IRenderer2D : IPixelRenderContext2D, IDisposable
{
    /// <summary>Top-left framebuffer offset of the aspect-ratio-constrained viewport.</summary>
    Vector2D<int> ViewportOffset { get; }

    RenderStatistics Statistics { get; }

    void Render(Camera camera, GameObject obj, GuiSystem? gui = null);
    void Resize(int width, int height);
}
