using Schreadt_Engine.Component.Logic;
using Silk.NET.Maths;

namespace Schreadt_Engine.Component;

public class Camera : GameObject
{
    private double _orthographicSize = 1.0;
    private Vector2D<double> _effectPositionOffset;
    private double _effectRotationOffset;

    public CameraController? Controller => GetComponent<CameraController>();

    public Vector2D<double> RenderPosition => Transform.WorldPosition + _effectPositionOffset;

    public double RenderRotationRadians => Transform.WorldRotation + _effectRotationOffset;

    public double ViewportAspectRatio { get; private set; } = 1.0;

    /// <summary>
    /// The number of world units visible from the center of the camera to the
    /// top or bottom edge of the viewport.
    /// </summary>
    public double OrthographicSize
    {
        get => _orthographicSize;
        set
        {
            if (!double.IsFinite(value) || value <= 0)
                throw new ArgumentOutOfRangeException(nameof(value), "Orthographic size must be finite and greater than zero.");

            _orthographicSize = value;
        }
    }

    /// <summary>
    /// Counter-clockwise camera rotation in radians.
    /// </summary>
    public double RotationRadians
    {
        get => Transform.WorldRotation;
        set => Transform.SetWorldRotation(value);
    }

    public T SetController<T>(T controller) where T : CameraController
    {
        ArgumentNullException.ThrowIfNull(controller);

        var previous = Controller;
        if (ReferenceEquals(previous, controller)) return controller;
        if (previous is null) return AddComponent(controller);

        RemoveComponent(previous);
        try
        {
            return AddComponent(controller);
        }
        catch
        {
            AddComponent(previous);
            throw;
        }
    }

    public bool ClearController()
    {
        var controller = Controller;
        return controller is not null && RemoveComponent(controller);
    }

    public Vector2D<double> WorldToViewportPoint(Vector2D<double> worldPosition, double aspectRatio)
    {
        var normalizedDevicePoint = CreateView(aspectRatio).WorldToNormalizedDevicePoint(worldPosition);
        return (normalizedDevicePoint + Vector2D<double>.One) * 0.5;
    }

    public Vector2D<double> ViewportToWorldPoint(Vector2D<double> viewportPosition, double aspectRatio)
    {
        var normalizedDevicePoint = viewportPosition * 2.0 - Vector2D<double>.One;
        return CreateView(aspectRatio).NormalizedDeviceToWorldPoint(normalizedDevicePoint);
    }

    internal CameraView CreateView(double aspectRatio)
    {
        if (!double.IsFinite(aspectRatio) || aspectRatio <= 0)
            throw new ArgumentOutOfRangeException(nameof(aspectRatio), "Aspect ratio must be finite and greater than zero.");

        ViewportAspectRatio = aspectRatio;
        return new CameraView(RenderPosition, OrthographicSize, aspectRatio, RenderRotationRadians);
    }

    internal CameraView CreateBackgroundView(double aspectRatio, IBackground2D background)
    {
        ArgumentNullException.ThrowIfNull(background);

        var factor = background.ParallaxFactor;
        if (!double.IsFinite(factor) || factor < 0.0)
            throw new ArgumentOutOfRangeException(
                nameof(background),
                "Background parallax factors must be finite and non-negative.");

        var origin = background.ParallaxOrigin;
        if (!double.IsFinite(origin.X) || !double.IsFinite(origin.Y))
            throw new ArgumentOutOfRangeException(
                nameof(background),
                "Background parallax origins must be finite.");

        if (!double.IsFinite(aspectRatio) || aspectRatio <= 0)
            throw new ArgumentOutOfRangeException(nameof(aspectRatio), "Aspect ratio must be finite and greater than zero.");

        ViewportAspectRatio = aspectRatio;
        var position = origin + ((RenderPosition - origin) * factor);
        return new CameraView(position, OrthographicSize, aspectRatio, RenderRotationRadians);
    }

    internal void SetEffectOffset(Vector2D<double> positionOffset, double rotationOffset)
    {
        if (!double.IsFinite(positionOffset.X) || !double.IsFinite(positionOffset.Y))
            throw new ArgumentOutOfRangeException(nameof(positionOffset), "Camera effect offsets must be finite.");
        if (!double.IsFinite(rotationOffset))
            throw new ArgumentOutOfRangeException(nameof(rotationOffset), "Camera effect rotation must be finite.");

        _effectPositionOffset = positionOffset;
        _effectRotationOffset = rotationOffset;
    }
}

public abstract class CameraController : GameComponent, IInitializable, IUpdateable, IShutdownable
{
    protected Camera Camera => (Camera)Owner;

    public virtual void Init()
    {
    }

    public abstract void Update(double dt);

    public virtual void Shutdown()
    {
    }

    protected override void OnAttached()
    {
        if (Owner is not Camera camera)
            throw new InvalidOperationException($"{nameof(CameraController)} components can only be attached to a camera.");
        if (camera.Controller is not null)
            throw new InvalidOperationException("A camera can only have one controller.");
    }
}

internal readonly struct CameraView
{
    private readonly Vector2D<double> _position;
    private readonly double _halfWidth;
    private readonly double _halfHeight;
    private readonly double _cosRotation;
    private readonly double _sinRotation;

    internal CameraView(Vector2D<double> position, double orthographicSize, double aspectRatio, double rotationRadians)
    {
        _position = position;
        _halfWidth = orthographicSize * aspectRatio;
        _halfHeight = orthographicSize;
        _cosRotation = Math.Cos(rotationRadians);
        _sinRotation = Math.Sin(rotationRadians);
    }

    internal Vector2D<double> Position => _position;

    internal double RotationRadians => Math.Atan2(_sinRotation, _cosRotation);

    internal double OrthographicSize => _halfHeight;

    internal double AspectRatio => _halfWidth / _halfHeight;

    internal (Vector2D<double> Minimum, Vector2D<double> Maximum) GetVisibleBounds()
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

    internal Vector2D<double> WorldToNormalizedDevicePoint(Vector2D<double> worldPosition)
    {
        var offset = worldPosition - _position;
        var viewX = offset.X * _cosRotation + offset.Y * _sinRotation;
        var viewY = -offset.X * _sinRotation + offset.Y * _cosRotation;
        return new Vector2D<double>(viewX / _halfWidth, viewY / _halfHeight);
    }

    internal Vector2D<double> NormalizedDeviceToWorldPoint(Vector2D<double> normalizedDevicePosition)
    {
        var viewX = normalizedDevicePosition.X * _halfWidth;
        var viewY = normalizedDevicePosition.Y * _halfHeight;
        var worldX = viewX * _cosRotation - viewY * _sinRotation;
        var worldY = viewX * _sinRotation + viewY * _cosRotation;
        return _position + new Vector2D<double>(worldX, worldY);
    }

    internal Vector2D<double> WorldRadiusToNormalizedDeviceScale(double radius)
    {
        return new Vector2D<double>(radius / _halfWidth, radius / _halfHeight);
    }
}
