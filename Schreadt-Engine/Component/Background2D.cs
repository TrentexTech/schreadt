using Schreadt_Engine.Core;
using Silk.NET.Maths;

namespace Schreadt_Engine.Component;

/// <summary>
/// Draws the content behind a scene. The renderer applies the configured
/// parallax transform before calling <see cref="Render"/>.
/// </summary>
public interface IBackground2D
{
    /// <summary>Whether this background should be rendered.</summary>
    bool Enabled { get; }

    /// <summary>
    /// How strongly the background follows camera movement. A value of one
    /// keeps the background in world space, while zero keeps it fixed relative
    /// to the camera. Values between zero and one create conventional depth.
    /// </summary>
    double ParallaxFactor { get; }

    /// <summary>The world-space point around which parallax movement is applied.</summary>
    Vector2D<double> ParallaxOrigin { get; }

    /// <summary>Draws the background using the active parallax-adjusted view.</summary>
    void Render(IBackgroundRenderContext2D context);
}
