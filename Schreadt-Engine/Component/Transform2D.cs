using Silk.NET.Maths;

namespace Schreadt_Engine.Component;

/// <summary>
/// Local and derived world-space transformation for a <see cref="GameObject"/>.
/// Rotations are counter-clockwise radians. World values are composed on demand
/// and are never exposed through mutable cached matrices.
/// </summary>
public sealed class Transform2D
{
    private readonly GameObject _owner;
    private Vector2D<double> _localPosition;
    private double _localRotation;
    private Vector2D<double> _localScale = Vector2D<double>.One;

    internal Transform2D(GameObject owner)
    {
        _owner = owner;
    }

    public Vector2D<double> LocalPosition
    {
        get => _localPosition;
        set => _localPosition = ValidatePosition(value, nameof(value));
    }

    public double LocalRotation
    {
        get => _localRotation;
        set => _localRotation = ValidateRotation(value, nameof(value));
    }

    public Vector2D<double> LocalScale
    {
        get => _localScale;
        set => _localScale = ValidateScale(value, nameof(value));
    }

    public Vector2D<double> WorldPosition
    {
        get
        {
            var parentTransform = _owner.Parent?.Transform;
            return parentTransform is null
                ? LocalPosition
                : parentTransform.TransformPoint(LocalPosition);
        }
    }

    public double WorldRotation => (_owner.Parent?.Transform.WorldRotation ?? 0.0) + LocalRotation;

    public Vector2D<double> WorldScale => Multiply(
        _owner.Parent?.Transform.WorldScale ?? Vector2D<double>.One,
        LocalScale);

    /// <summary>Transforms a point from this object's local space into world space.</summary>
    public Vector2D<double> TransformPoint(Vector2D<double> localPoint)
    {
        ValidatePosition(localPoint, nameof(localPoint));
        return WorldPosition + Rotate(Multiply(localPoint, WorldScale), WorldRotation);
    }

    /// <summary>Transforms a world-space point into this object's local space.</summary>
    public Vector2D<double> InverseTransformPoint(Vector2D<double> worldPoint)
    {
        ValidatePosition(worldPoint, nameof(worldPoint));
        return Divide(Rotate(worldPoint - WorldPosition, -WorldRotation), WorldScale);
    }

    internal TransformSnapshot CaptureLocal() => new(LocalPosition, LocalRotation, LocalScale);

    internal TransformSnapshot CaptureWorld() => new(WorldPosition, WorldRotation, WorldScale);

    internal void RestoreLocal(TransformSnapshot snapshot)
    {
        LocalPosition = snapshot.Position;
        LocalRotation = snapshot.Rotation;
        LocalScale = snapshot.Scale;
    }

    internal void SetLocalFromWorld(TransformSnapshot world, Transform2D? parent)
    {
        if (parent is null)
        {
            RestoreLocal(world);
            return;
        }

        LocalPosition = parent.InverseTransformPoint(world.Position);
        LocalRotation = world.Rotation - parent.WorldRotation;
        LocalScale = Divide(world.Scale, parent.WorldScale);
    }

    internal void SetWorldPosition(Vector2D<double> position)
    {
        LocalPosition = _owner.Parent?.Transform.InverseTransformPoint(position) ??
                        ValidatePosition(position, nameof(position));
    }

    internal void SetWorldRotation(double rotation)
    {
        rotation = ValidateRotation(rotation, nameof(rotation));
        LocalRotation = rotation - (_owner.Parent?.Transform.WorldRotation ?? 0.0);
    }

    internal void SetWorldScale(Vector2D<double> scale)
    {
        scale = ValidateScale(scale, nameof(scale));
        LocalScale = Divide(scale, _owner.Parent?.Transform.WorldScale ?? Vector2D<double>.One);
    }

    internal static Vector2D<double> Rotate(Vector2D<double> value, double radians)
    {
        var cosine = Math.Cos(radians);
        var sine = Math.Sin(radians);
        return new Vector2D<double>(
            value.X * cosine - value.Y * sine,
            value.X * sine + value.Y * cosine);
    }

    private static Vector2D<double> Multiply(Vector2D<double> first, Vector2D<double> second) =>
        new(first.X * second.X, first.Y * second.Y);

    private static Vector2D<double> Divide(Vector2D<double> value, Vector2D<double> divisor) =>
        new(value.X / divisor.X, value.Y / divisor.Y);

    private static Vector2D<double> ValidatePosition(Vector2D<double> value, string parameterName)
    {
        if (!double.IsFinite(value.X) || !double.IsFinite(value.Y))
            throw new ArgumentOutOfRangeException(parameterName, "Transform positions must be finite.");
        return value;
    }

    private static double ValidateRotation(double value, string parameterName)
    {
        if (!double.IsFinite(value))
            throw new ArgumentOutOfRangeException(parameterName, "Transform rotations must be finite radians.");
        return value;
    }

    private static Vector2D<double> ValidateScale(Vector2D<double> value, string parameterName)
    {
        if (!double.IsFinite(value.X) || !double.IsFinite(value.Y) || value.X <= 0.0 || value.Y <= 0.0)
            throw new ArgumentOutOfRangeException(parameterName, "Transform scale must be finite and positive.");
        return value;
    }
}

internal readonly record struct TransformSnapshot(
    Vector2D<double> Position,
    double Rotation,
    Vector2D<double> Scale);
