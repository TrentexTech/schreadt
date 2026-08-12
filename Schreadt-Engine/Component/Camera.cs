using Schreadt_Engine.Component.Logic;
using Schreadt_Engine.Core;
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

    internal CameraView2D CreateView(double aspectRatio)
    {
        if (!double.IsFinite(aspectRatio) || aspectRatio <= 0)
            throw new ArgumentOutOfRangeException(nameof(aspectRatio), "Aspect ratio must be finite and greater than zero.");

        ViewportAspectRatio = aspectRatio;
        return new CameraView2D(RenderPosition, OrthographicSize, aspectRatio, RenderRotationRadians);
    }

    internal CameraView2D CreateBackgroundView(double aspectRatio, IBackground2D background)
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
        return new CameraView2D(position, OrthographicSize, aspectRatio, RenderRotationRadians);
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
