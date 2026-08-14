using Schreadt_Engine.Core;
using Silk.NET.Maths;

namespace Schreadt_Engine.Collision;

public readonly record struct CollisionStatistics2D(
    int RegisteredColliderCount,
    int ActiveColliderCount,
    int RigidBodyCount,
    int PairCheckCount,
    int NarrowPhaseTestCount,
    int ContactCount,
    int ContactPointCount = 0,
    int PositionIterationCount = 1,
    int VelocityIterationCount = 1,
    double SolverMilliseconds = 0.0);

public sealed class CollisionDebugDraw2D
{
    private Vector4D<float> _staticColor = new(0.25f, 0.95f, 0.45f, 0.22f);
    private Vector4D<float> _kinematicColor = new(0.2f, 0.65f, 1.0f, 0.22f);
    private Vector4D<float> _dynamicColor = new(1.0f, 0.75f, 0.15f, 0.24f);
    private Vector4D<float> _triggerColor = new(0.9f, 0.25f, 1.0f, 0.22f);
    private Vector4D<float> _disabledColor = new(0.55f, 0.55f, 0.6f, 0.14f);

    public bool Enabled { get; set; }

    public bool ShowDisabled { get; set; }

    public Vector4D<float> StaticColor
    {
        get => _staticColor;
        set => _staticColor = ValidateColor(value, nameof(value));
    }

    public Vector4D<float> KinematicColor
    {
        get => _kinematicColor;
        set => _kinematicColor = ValidateColor(value, nameof(value));
    }

    public Vector4D<float> DynamicColor
    {
        get => _dynamicColor;
        set => _dynamicColor = ValidateColor(value, nameof(value));
    }

    public Vector4D<float> TriggerColor
    {
        get => _triggerColor;
        set => _triggerColor = ValidateColor(value, nameof(value));
    }

    public Vector4D<float> DisabledColor
    {
        get => _disabledColor;
        set => _disabledColor = ValidateColor(value, nameof(value));
    }

    internal void Draw(IRenderContext2D renderer, IReadOnlyList<Collider2D> colliders)
    {
        if (!Enabled) return;

        for (var index = 0; index < colliders.Count; index++)
        {
            var collider = colliders[index];
            if (!collider.Attached) continue;

            var active = collider.Enabled && collider.Owner.ActiveInHierarchy;
            if (!active && !ShowDisabled) continue;

            var color = active ? GetColor(collider) : DisabledColor;
            switch (collider)
            {
                case CircleCollider2D circle:
                    renderer.DrawCircle(circle.Center, circle.Radius, color);
                    break;

                case AxisAlignedBoxCollider2D box:
                    renderer.DrawRectangle(box.Center, box.Size, color);
                    break;

                case OrientedBoxCollider2D box:
                    renderer.DrawRectangle(box.Center, box.Size, color, box.WorldRotation);
                    break;
            }
        }
    }

    private Vector4D<float> GetColor(Collider2D collider)
    {
        if (collider.IsTrigger) return TriggerColor;
        return collider.BodyType switch
        {
            CollisionBodyType2D.Static => StaticColor,
            CollisionBodyType2D.Kinematic => KinematicColor,
            CollisionBodyType2D.Dynamic => DynamicColor,
            _ => StaticColor
        };
    }

    private static Vector4D<float> ValidateColor(Vector4D<float> color, string parameterName)
    {
        if (!float.IsFinite(color.X) || !float.IsFinite(color.Y) ||
            !float.IsFinite(color.Z) || !float.IsFinite(color.W))
        {
            throw new ArgumentOutOfRangeException(parameterName, "Debug colors must be finite.");
        }

        return color;
    }
}
