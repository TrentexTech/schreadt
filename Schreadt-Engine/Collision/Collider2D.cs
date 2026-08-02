using Schreadt_Engine.Component;
using Silk.NET.Maths;

namespace Schreadt_Engine.Collision;

public enum CollisionBodyType2D
{
    Static,
    Dynamic
}

public readonly record struct CollisionContact2D(
    Collider2D Other,
    Vector2D<double> Normal,
    double Penetration);

public abstract class Collider2D
{
    private static long _nextId;

    internal long Id { get; } = Interlocked.Increment(ref _nextId);
    internal CollisionWorld2D? World { get; set; }

    protected Collider2D(GameObject owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        Owner = owner;
    }

    public GameObject Owner { get; }

    public CollisionBodyType2D BodyType { get; set; } = CollisionBodyType2D.Static;

    public bool Enabled { get; set; } = true;

    public bool IsTrigger { get; set; }

    public event Action<CollisionContact2D>? CollisionEntered;

    public event Action<CollisionContact2D>? CollisionStayed;

    public event Action<CollisionContact2D>? CollisionExited;

    internal void NotifyEntered(CollisionContact2D contact) => CollisionEntered?.Invoke(contact);

    internal void NotifyStayed(CollisionContact2D contact) => CollisionStayed?.Invoke(contact);

    internal void NotifyExited(CollisionContact2D contact) => CollisionExited?.Invoke(contact);
}

public sealed class CircleCollider2D : Collider2D
{
    private double _radius;

    public CircleCollider2D(GameObject owner, double radius) : base(owner)
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

    public Vector2D<double> Center => Owner.Position + Offset;
}
