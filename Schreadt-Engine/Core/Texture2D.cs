namespace Schreadt_Engine.Core;

public sealed class Texture2D
{
    internal uint Handle { get; }
    internal TextureSampling? CurrentSampling { get; set; }

    public string AssetId { get; }
    public int Width { get; }
    public int Height { get; }

    internal Texture2D(uint handle, string assetId, int width, int height)
    {
        Handle = handle;
        AssetId = assetId;
        Width = width;
        Height = height;
    }
}

public enum TextureSampling
{
    Nearest,
    Linear
}

public readonly record struct TextureRegion(float Left, float Top, float Right, float Bottom)
{
    public static TextureRegion Full { get; } = new(0.0f, 0.0f, 1.0f, 1.0f);

    /// <summary>Creates a normalized region for one cell in a uniformly divided sprite sheet.</summary>
    public static TextureRegion FromGridCell(int column, int row, int columns, int rows)
    {
        if (columns <= 0) throw new ArgumentOutOfRangeException(nameof(columns), "Column count must be greater than zero.");
        if (rows <= 0) throw new ArgumentOutOfRangeException(nameof(rows), "Row count must be greater than zero.");
        if (column < 0 || column >= columns) throw new ArgumentOutOfRangeException(nameof(column));
        if (row < 0 || row >= rows) throw new ArgumentOutOfRangeException(nameof(row));

        var cellWidth = 1.0f / columns;
        var cellHeight = 1.0f / rows;
        return new TextureRegion(
            column * cellWidth,
            row * cellHeight,
            (column + 1) * cellWidth,
            (row + 1) * cellHeight);
    }

    public void Validate()
    {
        if (!float.IsFinite(Left) || !float.IsFinite(Top) || !float.IsFinite(Right) || !float.IsFinite(Bottom) ||
            Left < 0.0f || Top < 0.0f || Right > 1.0f || Bottom > 1.0f || Right <= Left || Bottom <= Top)
            throw new ArgumentOutOfRangeException(nameof(TextureRegion), "Texture coordinates must form a non-empty normalized region.");
    }
}
