using Schreadt_Engine.Component;
using Silk.NET.Maths;

namespace Schreadt_Engine.Collision;

public enum CollisionBodyType2D
{
    Static,
    Kinematic,
    Dynamic
}

public sealed class RigidBody2D : GameComponent
{
    private Vector2D<double> _velocity;
    private double _gravityScale = 1.0;
    private double _mass = 1.0;
    private double _restitution;

    internal CollisionWorld2D? World { get; set; }

    public CollisionBodyType2D BodyType { get; set; } = CollisionBodyType2D.Static;

    public bool UseGravity { get; set; } = true;

    public Vector2D<double> Velocity
    {
        get => _velocity;
        set
        {
            EnsureFinite(value, nameof(value));
            _velocity = value;
        }
    }

    public double GravityScale
    {
        get => _gravityScale;
        set
        {
            if (!double.IsFinite(value))
                throw new ArgumentOutOfRangeException(nameof(value), "Gravity scale must be finite.");

            _gravityScale = value;
        }
    }

    public double Mass
    {
        get => _mass;
        set
        {
            if (!double.IsFinite(value) || value <= 0)
                throw new ArgumentOutOfRangeException(nameof(value), "Mass must be finite and greater than zero.");

            _mass = value;
        }
    }

    public double Restitution
    {
        get => _restitution;
        set
        {
            if (!double.IsFinite(value) || value is < 0 or > 1)
                throw new ArgumentOutOfRangeException(nameof(value), "Restitution must be between zero and one.");

            _restitution = value;
        }
    }

    internal double InverseMass => BodyType == CollisionBodyType2D.Dynamic ? 1.0 / Mass : 0.0;

    internal void Integrate(Vector2D<double> gravity, double dt)
    {
        if (BodyType == CollisionBodyType2D.Static || !Owner.Active) return;

        if (BodyType == CollisionBodyType2D.Dynamic && UseGravity)
            Velocity += gravity * (GravityScale * dt);
        Owner.Move(Velocity * dt);
    }

    protected override void OnAttached()
    {
        if (Owner.GetComponent<RigidBody2D>() is not null)
            throw new InvalidOperationException("A game object can only have one rigid body.");
    }

    internal override void ValidateCanDetach()
    {
        if (Owner.GetComponents<Collider2D>().Count > 0)
            throw new InvalidOperationException("Remove the game object's colliders before removing its rigid body.");
    }

    private static void EnsureFinite(Vector2D<double> value, string parameterName)
    {
        if (!double.IsFinite(value.X) || !double.IsFinite(value.Y))
            throw new ArgumentOutOfRangeException(parameterName, "Vector components must be finite.");
    }
}
