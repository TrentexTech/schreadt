using Schreadt_Engine.Component.Logic;
using Silk.NET.Maths;

namespace Schreadt_Engine.Component;

public readonly record struct CameraBounds2D
{
    public Vector2D<double> Minimum { get; }

    public Vector2D<double> Maximum { get; }

    public CameraBounds2D(Vector2D<double> minimum, Vector2D<double> maximum)
    {
        if (!double.IsFinite(minimum.X) || !double.IsFinite(minimum.Y) ||
            !double.IsFinite(maximum.X) || !double.IsFinite(maximum.Y))
        {
            throw new ArgumentOutOfRangeException(nameof(minimum), "Camera bounds must be finite.");
        }

        if (minimum.X > maximum.X || minimum.Y > maximum.Y)
            throw new ArgumentException("Camera bounds minimum must not exceed its maximum.", nameof(maximum));

        Minimum = minimum;
        Maximum = maximum;
    }

    internal Vector2D<double> ClampViewCenter(Camera camera, Vector2D<double> position)
    {
        var halfHeight = camera.OrthographicSize;
        var halfWidth = halfHeight * camera.ViewportAspectRatio;
        var cosine = Math.Abs(Math.Cos(camera.RotationRadians));
        var sine = Math.Abs(Math.Sin(camera.RotationRadians));
        var horizontalExtent = halfWidth * cosine + halfHeight * sine;
        var verticalExtent = halfWidth * sine + halfHeight * cosine;

        return new Vector2D<double>(
            ClampAxis(position.X, Minimum.X, Maximum.X, horizontalExtent),
            ClampAxis(position.Y, Minimum.Y, Maximum.Y, verticalExtent));
    }

    private static double ClampAxis(double value, double minimum, double maximum, double viewExtent)
    {
        var availableSize = maximum - minimum;
        if (availableSize <= viewExtent * 2.0) return (minimum + maximum) * 0.5;
        return Math.Clamp(value, minimum + viewExtent, maximum - viewExtent);
    }
}

public sealed class FollowCameraController2D : CameraController
{
    private GameObject _target;
    private Vector2D<double> _targetOffset;
    private Vector2D<double> _deadZone;
    private double _smoothTime = 0.15;

    public FollowCameraController2D(GameObject target)
    {
        ArgumentNullException.ThrowIfNull(target);
        _target = target;
    }

    public GameObject Target
    {
        get => _target;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _target = value;
        }
    }

    public Vector2D<double> TargetOffset
    {
        get => _targetOffset;
        set
        {
            EnsureFinite(value, nameof(value));
            _targetOffset = value;
        }
    }

    /// <summary>Full width and height in which the target can move without moving the camera.</summary>
    public Vector2D<double> DeadZone
    {
        get => _deadZone;
        set
        {
            EnsureFinite(value, nameof(value));
            if (value.X < 0.0 || value.Y < 0.0)
                throw new ArgumentOutOfRangeException(nameof(value), "Camera dead-zone dimensions must be non-negative.");
            _deadZone = value;
        }
    }

    /// <summary>Approximate time in seconds for the camera to settle. Zero snaps immediately.</summary>
    public double SmoothTime
    {
        get => _smoothTime;
        set
        {
            if (!double.IsFinite(value) || value < 0.0)
                throw new ArgumentOutOfRangeException(nameof(value), "Camera smooth time must be finite and non-negative.");
            _smoothTime = value;
        }
    }

    public bool SnapOnInit { get; set; } = true;

    public CameraBounds2D? WorldBounds { get; set; }

    public override void Init()
    {
        if (SnapOnInit) SnapToTarget();
    }

    public override void Update(double dt)
    {
        if (!double.IsFinite(dt) || dt < 0.0)
            throw new ArgumentOutOfRangeException(nameof(dt), "Delta time must be finite and non-negative.");

        var targetPosition = Target.Position + TargetOffset;
        var desiredPosition = ApplyDeadZone(Camera.Position, targetPosition);
        desiredPosition = ClampToBounds(desiredPosition);

        if (SmoothTime <= 0.0 || dt <= 0.0)
        {
            if (SmoothTime <= 0.0) Camera.Position = desiredPosition;
            return;
        }

        var interpolation = 1.0 - Math.Exp(-dt / SmoothTime);
        Camera.Position = ClampToBounds(Camera.Position + ((desiredPosition - Camera.Position) * interpolation));
    }

    public void SnapToTarget()
    {
        Camera.Position = ClampToBounds(Target.Position + TargetOffset);
    }

    private Vector2D<double> ApplyDeadZone(
        Vector2D<double> cameraPosition,
        Vector2D<double> targetPosition)
    {
        var halfDeadZone = DeadZone * 0.5;
        var offset = targetPosition - cameraPosition;
        var result = cameraPosition;

        if (offset.X > halfDeadZone.X) result.X = targetPosition.X - halfDeadZone.X;
        else if (offset.X < -halfDeadZone.X) result.X = targetPosition.X + halfDeadZone.X;

        if (offset.Y > halfDeadZone.Y) result.Y = targetPosition.Y - halfDeadZone.Y;
        else if (offset.Y < -halfDeadZone.Y) result.Y = targetPosition.Y + halfDeadZone.Y;

        return result;
    }

    private Vector2D<double> ClampToBounds(Vector2D<double> position)
    {
        return WorldBounds?.ClampViewCenter(Camera, position) ?? position;
    }

    private static void EnsureFinite(Vector2D<double> value, string parameterName)
    {
        if (!double.IsFinite(value.X) || !double.IsFinite(value.Y))
            throw new ArgumentOutOfRangeException(parameterName, "Camera vectors must be finite.");
    }
}

public sealed class CameraShake2D : GameComponent, IUpdateable, IShutdownable
{
    private double _duration;
    private double _elapsed;
    private double _positionMagnitude;
    private double _rotationMagnitude;
    private double _frequency;

    private Camera Camera => (Camera)Owner;

    public bool IsShaking { get; private set; }

    public double RemainingTime => Math.Max(0.0, _duration - _elapsed);

    public void Shake(
        double duration,
        double positionMagnitude,
        double rotationMagnitude = 0.0,
        double frequency = 24.0)
    {
        if (!double.IsFinite(duration) || duration <= 0.0)
            throw new ArgumentOutOfRangeException(nameof(duration), "Shake duration must be finite and greater than zero.");
        if (!double.IsFinite(positionMagnitude) || positionMagnitude < 0.0)
            throw new ArgumentOutOfRangeException(nameof(positionMagnitude), "Shake magnitude must be finite and non-negative.");
        if (!double.IsFinite(rotationMagnitude) || rotationMagnitude < 0.0)
            throw new ArgumentOutOfRangeException(nameof(rotationMagnitude), "Shake rotation must be finite and non-negative.");
        if (!double.IsFinite(frequency) || frequency <= 0.0)
            throw new ArgumentOutOfRangeException(nameof(frequency), "Shake frequency must be finite and greater than zero.");

        _duration = duration;
        _elapsed = 0.0;
        _positionMagnitude = positionMagnitude;
        _rotationMagnitude = rotationMagnitude;
        _frequency = frequency;
        IsShaking = true;
        ApplySample();
    }

    public void Update(double dt)
    {
        if (!double.IsFinite(dt) || dt < 0.0)
            throw new ArgumentOutOfRangeException(nameof(dt), "Delta time must be finite and non-negative.");
        if (!IsShaking || dt == 0.0) return;

        _elapsed = Math.Min(_duration, _elapsed + dt);
        if (_elapsed >= _duration)
        {
            Stop();
            return;
        }

        ApplySample();
    }

    public void Stop()
    {
        IsShaking = false;
        _elapsed = _duration;
        if (Attached) Camera.SetEffectOffset(Vector2D<double>.Zero, 0.0);
    }

    public void Shutdown() => Stop();

    protected override void OnAttached()
    {
        if (Owner is not Camera camera)
            throw new InvalidOperationException($"{nameof(CameraShake2D)} components can only be attached to a camera.");
        if (camera.GetComponent<CameraShake2D>() is not null)
            throw new InvalidOperationException("A camera can only have one shake component.");
    }

    protected override void OnDetached()
    {
        Camera.SetEffectOffset(Vector2D<double>.Zero, 0.0);
        IsShaking = false;
    }

    private void ApplySample()
    {
        var envelope = 1.0 - (_elapsed / _duration);
        var phase = _elapsed * _frequency * Math.PI * 2.0;
        var positionOffset = new Vector2D<double>(
            Math.Sin(phase + 0.73),
            Math.Sin((phase * 1.37) + 2.11)) * (_positionMagnitude * envelope);
        var rotationOffset = Math.Sin((phase * 0.83) + 4.17) * _rotationMagnitude * envelope;
        Camera.SetEffectOffset(positionOffset, rotationOffset);
    }
}
