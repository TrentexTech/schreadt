using Silk.NET.Maths;

namespace Schreadt_Engine.Core;

/// <summary>Insertion points available to scene-owned frame composition passes.</summary>
public enum FrameCompositionStage
{
    BeforeScene,
    AfterScene,
    BeforeGui
}

/// <summary>
/// Drawing context supplied to registered frame composition passes. World-space
/// drawing uses <see cref="View"/>, while screen drawing uses viewport pixels.
/// </summary>
public interface IFrameCompositionContext2D : IPixelRenderContext2D
{
    CameraView2D View { get; }

    void DrawLines(IReadOnlyList<LineSegment2D> lines, Vector4D<float> color);
}

/// <summary>A scene-owned drawing pass inserted at a defined frame composition stage.</summary>
public interface IFrameCompositionPass2D
{
    string Name { get; }
    FrameCompositionStage Stage { get; }
    int Order { get; }
    bool Enabled { get; }

    void Render(IFrameCompositionContext2D context);
}

/// <summary>Elapsed rendering time for one registered composition pass.</summary>
public readonly record struct FrameCompositionPassTiming(
    string Name,
    FrameCompositionStage Stage,
    double ElapsedMilliseconds);

/// <summary>Timing information for the most recently composed frame.</summary>
public sealed class FrameCompositionStatistics
{
    private static readonly IReadOnlyList<FrameCompositionPassTiming> NoPassTimings =
        Array.Empty<FrameCompositionPassTiming>();

    public static FrameCompositionStatistics Empty { get; } = new(
        0.0,
        0.0,
        0.0,
        0.0,
        0.0,
        0.0,
        0.0,
        0.0,
        NoPassTimings);

    public double TotalMilliseconds { get; }
    public double BackgroundMilliseconds { get; }
    public double BeforeSceneMilliseconds { get; }
    public double SceneMilliseconds { get; }
    public double AfterSceneMilliseconds { get; }
    public double DiagnosticsMilliseconds { get; }
    public double BeforeGuiMilliseconds { get; }
    public double GuiMilliseconds { get; }
    public IReadOnlyList<FrameCompositionPassTiming> PassTimings { get; }

    public FrameCompositionStatistics(
        double totalMilliseconds,
        double backgroundMilliseconds,
        double beforeSceneMilliseconds,
        double sceneMilliseconds,
        double afterSceneMilliseconds,
        double diagnosticsMilliseconds,
        double beforeGuiMilliseconds,
        double guiMilliseconds,
        IReadOnlyList<FrameCompositionPassTiming> passTimings)
    {
        ValidateElapsed(totalMilliseconds, nameof(totalMilliseconds));
        ValidateElapsed(backgroundMilliseconds, nameof(backgroundMilliseconds));
        ValidateElapsed(beforeSceneMilliseconds, nameof(beforeSceneMilliseconds));
        ValidateElapsed(sceneMilliseconds, nameof(sceneMilliseconds));
        ValidateElapsed(afterSceneMilliseconds, nameof(afterSceneMilliseconds));
        ValidateElapsed(diagnosticsMilliseconds, nameof(diagnosticsMilliseconds));
        ValidateElapsed(beforeGuiMilliseconds, nameof(beforeGuiMilliseconds));
        ValidateElapsed(guiMilliseconds, nameof(guiMilliseconds));
        ArgumentNullException.ThrowIfNull(passTimings);

        TotalMilliseconds = totalMilliseconds;
        BackgroundMilliseconds = backgroundMilliseconds;
        BeforeSceneMilliseconds = beforeSceneMilliseconds;
        SceneMilliseconds = sceneMilliseconds;
        AfterSceneMilliseconds = afterSceneMilliseconds;
        DiagnosticsMilliseconds = diagnosticsMilliseconds;
        BeforeGuiMilliseconds = beforeGuiMilliseconds;
        GuiMilliseconds = guiMilliseconds;
        PassTimings = passTimings.ToArray();
    }

    private static void ValidateElapsed(double elapsed, string parameterName)
    {
        if (!double.IsFinite(elapsed) || elapsed < 0.0)
            throw new ArgumentOutOfRangeException(parameterName, "Elapsed time must be finite and non-negative.");
    }
}
