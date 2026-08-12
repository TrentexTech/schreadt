using Schreadt_Engine.Core;
using Silk.NET.Maths;

namespace Schreadt_Engine.Component;

/// <summary>A configurable world-space grid background supplied by the engine.</summary>
public sealed class GridBackground2D : IBackground2D
{
    private const int MaximumGridLineCount = 2048;
    private double _cellSize = 0.25;
    private int _majorLineEvery = 4;
    private double _parallaxFactor = 1.0;
    private Vector2D<double> _parallaxOrigin;

    public bool Enabled { get; set; } = true;

    public double ParallaxFactor
    {
        get => _parallaxFactor;
        set
        {
            if (!double.IsFinite(value) || value < 0.0)
                throw new ArgumentOutOfRangeException(nameof(value), "Parallax factor must be finite and non-negative.");
            _parallaxFactor = value;
        }
    }

    public Vector2D<double> ParallaxOrigin
    {
        get => _parallaxOrigin;
        set
        {
            if (!double.IsFinite(value.X) || !double.IsFinite(value.Y))
                throw new ArgumentOutOfRangeException(nameof(value), "Parallax origin must be finite.");
            _parallaxOrigin = value;
        }
    }

    public double CellSize
    {
        get => _cellSize;
        set
        {
            if (!double.IsFinite(value) || value <= 0)
                throw new ArgumentOutOfRangeException(nameof(value), "Grid cell size must be finite and greater than zero.");

            _cellSize = value;
        }
    }

    public int MajorLineEvery
    {
        get => _majorLineEvery;
        set
        {
            if (value <= 0)
                throw new ArgumentOutOfRangeException(nameof(value), "Major-line interval must be greater than zero.");

            _majorLineEvery = value;
        }
    }

    public Vector4D<float> MinorLineColor { get; set; } = new(0.32f, 0.38f, 0.5f, 0.16f);

    public Vector4D<float> MajorLineColor { get; set; } = new(0.42f, 0.5f, 0.68f, 0.28f);

    public Vector4D<float> OriginAxisColor { get; set; } = new(0.58f, 0.67f, 0.9f, 0.48f);

    public void Render(IBackgroundRenderContext2D context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var minimum = context.View.VisibleMinimum;
        var maximum = context.View.VisibleMaximum;
        var estimatedLineCount = (maximum.X - minimum.X + maximum.Y - minimum.Y) / CellSize + 4.0;
        var indexStride = Math.Max(1L, (long)Math.Ceiling(estimatedLineCount / MaximumGridLineCount));
        var minimumXIndex = (long)Math.Floor(minimum.X / CellSize);
        var maximumXIndex = (long)Math.Ceiling(maximum.X / CellSize);
        var minimumYIndex = (long)Math.Floor(minimum.Y / CellSize);
        var maximumYIndex = (long)Math.Ceiling(maximum.Y / CellSize);
        var minorLines = new List<LineSegment2D>();
        var majorLines = new List<LineSegment2D>();
        var axisLines = new List<LineSegment2D>();

        for (var index = FirstMultipleAtOrAbove(minimumXIndex, indexStride);
             index <= maximumXIndex;
             index += indexStride)
        {
            var x = index * CellSize;
            SelectGridBatch(index, minorLines, majorLines, axisLines).Add(new LineSegment2D(
                new Vector2D<double>(x, minimum.Y),
                new Vector2D<double>(x, maximum.Y)));
        }

        for (var index = FirstMultipleAtOrAbove(minimumYIndex, indexStride);
             index <= maximumYIndex;
             index += indexStride)
        {
            var y = index * CellSize;
            SelectGridBatch(index, minorLines, majorLines, axisLines).Add(new LineSegment2D(
                new Vector2D<double>(minimum.X, y),
                new Vector2D<double>(maximum.X, y)));
        }

        context.DrawLines(minorLines, MinorLineColor);
        context.DrawLines(majorLines, MajorLineColor);
        context.DrawLines(axisLines, OriginAxisColor);
    }

    private List<LineSegment2D> SelectGridBatch(
        long lineIndex,
        List<LineSegment2D> minorLines,
        List<LineSegment2D> majorLines,
        List<LineSegment2D> axisLines)
    {
        if (lineIndex == 0) return axisLines;
        return lineIndex % MajorLineEvery == 0 ? majorLines : minorLines;
    }

    private static long FirstMultipleAtOrAbove(long value, long interval)
    {
        return (long)(Math.Ceiling(value / (double)interval) * interval);
    }
}
