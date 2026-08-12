using Silk.NET.Maths;

namespace Schreadt_Engine.Core;

/// <summary>An immutable, backend-independent description of a two-dimensional camera view.</summary>
public readonly struct CameraView2D
{
    private readonly Vector2D<double> _center;
    private readonly double _halfWidth;
    private readonly double _halfHeight;
    private readonly double _cosRotation;
    private readonly double _sinRotation;

    public CameraView2D(
        Vector2D<double> center,
        double orthographicSize,
        double aspectRatio,
        double rotationRadians = 0.0)
    {
        if (!double.IsFinite(center.X) || !double.IsFinite(center.Y))
            throw new ArgumentOutOfRangeException(nameof(center), "Camera view center must be finite.");
        if (!double.IsFinite(orthographicSize) || orthographicSize <= 0.0)
            throw new ArgumentOutOfRangeException(
                nameof(orthographicSize),
                "Camera view orthographic size must be finite and greater than zero.");
        if (!double.IsFinite(aspectRatio) || aspectRatio <= 0.0)
            throw new ArgumentOutOfRangeException(
                nameof(aspectRatio),
                "Camera view aspect ratio must be finite and greater than zero.");
        if (!double.IsFinite(rotationRadians))
            throw new ArgumentOutOfRangeException(nameof(rotationRadians), "Camera view rotation must be finite.");

        _center = center;
        _halfWidth = orthographicSize * aspectRatio;
        _halfHeight = orthographicSize;
        _cosRotation = Math.Cos(rotationRadians);
        _sinRotation = Math.Sin(rotationRadians);
    }

    public Vector2D<double> Center => _center;

    public double RotationRadians => Math.Atan2(_sinRotation, _cosRotation);

    public double OrthographicSize => _halfHeight;

    public double AspectRatio => _halfWidth / _halfHeight;

    public (Vector2D<double> Minimum, Vector2D<double> Maximum) GetVisibleBounds()
    {
        var bottomLeft = NormalizedDeviceToWorldPoint(new Vector2D<double>(-1.0, -1.0));
        var topLeft = NormalizedDeviceToWorldPoint(new Vector2D<double>(-1.0, 1.0));
        var bottomRight = NormalizedDeviceToWorldPoint(new Vector2D<double>(1.0, -1.0));
        var topRight = NormalizedDeviceToWorldPoint(new Vector2D<double>(1.0, 1.0));
        return (
            new Vector2D<double>(
                Math.Min(Math.Min(bottomLeft.X, topLeft.X), Math.Min(bottomRight.X, topRight.X)),
                Math.Min(Math.Min(bottomLeft.Y, topLeft.Y), Math.Min(bottomRight.Y, topRight.Y))),
            new Vector2D<double>(
                Math.Max(Math.Max(bottomLeft.X, topLeft.X), Math.Max(bottomRight.X, topRight.X)),
                Math.Max(Math.Max(bottomLeft.Y, topLeft.Y), Math.Max(bottomRight.Y, topRight.Y))));
    }

    public Vector2D<double> WorldToNormalizedDevicePoint(Vector2D<double> worldPosition)
    {
        var offset = worldPosition - _center;
        var viewX = offset.X * _cosRotation + offset.Y * _sinRotation;
        var viewY = -offset.X * _sinRotation + offset.Y * _cosRotation;
        return new Vector2D<double>(viewX / _halfWidth, viewY / _halfHeight);
    }

    public Vector2D<double> NormalizedDeviceToWorldPoint(Vector2D<double> normalizedDevicePosition)
    {
        var viewX = normalizedDevicePosition.X * _halfWidth;
        var viewY = normalizedDevicePosition.Y * _halfHeight;
        var worldX = viewX * _cosRotation - viewY * _sinRotation;
        var worldY = viewX * _sinRotation + viewY * _cosRotation;
        return _center + new Vector2D<double>(worldX, worldY);
    }

    public Vector2D<double> WorldRadiusToNormalizedDeviceScale(double radius)
    {
        return new Vector2D<double>(radius / _halfWidth, radius / _halfHeight);
    }
}
