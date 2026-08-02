using Silk.NET.Maths;

namespace Schreadt_Engine.Component;

public sealed class GridBackground2D
{
    private double _cellSize = 0.25;
    private int _majorLineEvery = 4;

    public bool Enabled { get; set; } = true;

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
}
