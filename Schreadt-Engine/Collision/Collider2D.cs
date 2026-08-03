using Schreadt_Engine.Component;
using Silk.NET.Maths;

namespace Schreadt_Engine.Collision;

public readonly record struct CollisionContact2D(
    Collider2D Other,
    Vector2D<double> Normal,
    double Penetration);

public abstract class Collider2D : GameComponent, ICollisionShape2D
{
    private static long _nextId;
    private RigidBody2D? _body;

    internal long Id { get; } = Interlocked.Increment(ref _nextId);
    internal CollisionWorld2D? World { get; set; }

    public RigidBody2D Body => _body
        ?? throw new InvalidOperationException("The collider is not attached to a rigid body.");

    public CollisionBodyType2D BodyType
    {
        get => Body.BodyType;
        set => Body.BodyType = value;
    }

    public bool Enabled { get; set; } = true;

    public bool IsTrigger { get; set; }

    public abstract Vector2D<double> Center { get; }

    public event Action<CollisionContact2D>? CollisionEntered;

    public event Action<CollisionContact2D>? CollisionStayed;

    public event Action<CollisionContact2D>? CollisionExited;

    internal void NotifyEntered(CollisionContact2D contact) => CollisionEntered?.Invoke(contact);

    internal void NotifyStayed(CollisionContact2D contact) => CollisionStayed?.Invoke(contact);

    internal void NotifyExited(CollisionContact2D contact) => CollisionExited?.Invoke(contact);

    protected override void OnAttached()
    {
        _body = Owner.GetComponent<RigidBody2D>() ?? Owner.AddComponent(new RigidBody2D());
    }

    protected override void OnDetached()
    {
        _body = null;
    }
}

public sealed class CircleCollider2D : Collider2D
{
    private double _radius;

    public CircleCollider2D(double radius)
    {
        Radius = radius;
    }

    public double Radius
    {
        get => _radius;
        set
        {
            if (!double.IsFinite(value) || value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Collider radius must be finite and greater than zero.");
            }

            _radius = value;
        }
    }

    public Vector2D<double> Offset { get; set; }

    public override Vector2D<double> Center => Owner.Position + Offset;
}
