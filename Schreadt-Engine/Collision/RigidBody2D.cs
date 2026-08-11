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
    private const double CollisionWakeThresholdSquared = 1e-10;

    private CollisionBodyType2D _bodyType;
    private Vector2D<double> _velocity;
    private Vector2D<double> _accumulatedForce;
    private bool _useGravity = true;
    private bool _allowSleep = true;
    private double _gravityScale = 1.0;
    private double _mass = 1.0;
    private double _restitution;
    private double _friction = 0.5;
    private double _linearDamping;
    private double _maximumSpeed = double.PositiveInfinity;
    private double _sleepVelocityThreshold = 0.02;
    private double _timeToSleep = 0.5;
    private double _sleepTimer;

    internal CollisionWorld2D? World { get; set; }

    public CollisionBodyType2D BodyType
    {
        get => _bodyType;
        set
        {
            if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
            if (_bodyType == value) return;

            _bodyType = value;
            WakeUp();
            if (value != CollisionBodyType2D.Dynamic) _accumulatedForce = Vector2D<double>.Zero;
        }
    }

    public bool UseGravity
    {
        get => _useGravity;
        set
        {
            if (_useGravity == value) return;
            _useGravity = value;
            WakeUp();
        }
    }

    public Vector2D<double> Velocity
    {
        get => _velocity;
        set
        {
            EnsureFinite(value, nameof(value));
            _velocity = value;
            ClampVelocity();
            WakeUp();
        }
    }

    public Vector2D<double> AccumulatedForce => _accumulatedForce;

    public bool IsSleeping { get; private set; }

    public bool AllowSleep
    {
        get => _allowSleep;
        set
        {
            _allowSleep = value;
            if (!value) WakeUp();
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
            WakeUp();
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
            WakeUp();
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

    public double Friction
    {
        get => _friction;
        set
        {
            if (!double.IsFinite(value) || value < 0)
                throw new ArgumentOutOfRangeException(nameof(value), "Friction must be finite and non-negative.");

            _friction = value;
        }
    }

    public double LinearDamping
    {
        get => _linearDamping;
        set
        {
            if (!double.IsFinite(value) || value < 0)
                throw new ArgumentOutOfRangeException(nameof(value), "Linear damping must be finite and non-negative.");

            _linearDamping = value;
            WakeUp();
        }
    }

    public double MaximumSpeed
    {
        get => _maximumSpeed;
        set
        {
            if ((!double.IsFinite(value) && !double.IsPositiveInfinity(value)) || value <= 0)
                throw new ArgumentOutOfRangeException(nameof(value), "Maximum speed must be positive or positive infinity.");

            _maximumSpeed = value;
            ClampVelocity();
        }
    }

    public double SleepVelocityThreshold
    {
        get => _sleepVelocityThreshold;
        set
        {
            if (!double.IsFinite(value) || value < 0)
                throw new ArgumentOutOfRangeException(nameof(value), "Sleep velocity threshold must be finite and non-negative.");

            _sleepVelocityThreshold = value;
            WakeUp();
        }
    }

    public double TimeToSleep
    {
        get => _timeToSleep;
        set
        {
            if (!double.IsFinite(value) || value < 0)
                throw new ArgumentOutOfRangeException(nameof(value), "Time to sleep must be finite and non-negative.");

            _timeToSleep = value;
            WakeUp();
        }
    }

    internal double InverseMass => BodyType == CollisionBodyType2D.Dynamic ? 1.0 / Mass : 0.0;

    public void AddForce(Vector2D<double> force)
    {
        EnsureFinite(force, nameof(force));
        if (BodyType != CollisionBodyType2D.Dynamic) return;

        _accumulatedForce += force;
        WakeUp();
    }

    public void AddImpulse(Vector2D<double> impulse)
    {
        EnsureFinite(impulse, nameof(impulse));
        if (BodyType != CollisionBodyType2D.Dynamic) return;

        _velocity += impulse * InverseMass;
        ClampVelocity();
        WakeUp();
    }

    public void ClearForces()
    {
        _accumulatedForce = Vector2D<double>.Zero;
    }

    public void WakeUp()
    {
        IsSleeping = false;
        _sleepTimer = 0.0;
    }

    public void Sleep()
    {
        if (BodyType != CollisionBodyType2D.Dynamic || !AllowSleep) return;

        IsSleeping = true;
        _sleepTimer = TimeToSleep;
        _velocity = Vector2D<double>.Zero;
        _accumulatedForce = Vector2D<double>.Zero;
    }

    internal void Integrate(Vector2D<double> gravity, double dt)
    {
        if (BodyType == CollisionBodyType2D.Static || !Owner.ActiveInHierarchy)
        {
            ClearForces();
            return;
        }

        if (BodyType == CollisionBodyType2D.Dynamic && IsSleeping)
        {
            ClearForces();
            return;
        }

        if (BodyType == CollisionBodyType2D.Dynamic)
        {
            var acceleration = _accumulatedForce * InverseMass;
            if (UseGravity) acceleration += gravity * GravityScale;

            _velocity += acceleration * dt;

            if (LinearDamping > 0)
            {
                _velocity *= Math.Exp(-LinearDamping * dt);
            }

            ClampVelocity();
        }

        Owner.Move(_velocity * dt);
        ClearForces();
    }

    internal void ApplyCollisionImpulse(Vector2D<double> impulse)
    {
        if (BodyType != CollisionBodyType2D.Dynamic) return;

        var impulseLengthSquared = impulse.X * impulse.X + impulse.Y * impulse.Y;
        if (IsSleeping && impulseLengthSquared > CollisionWakeThresholdSquared) WakeUp();

        _velocity += impulse * InverseMass;
        ClampVelocity();
    }

    internal void ApplyPositionCorrection(Vector2D<double> correction)
    {
        if (correction == Vector2D<double>.Zero) return;

        if (IsSleeping)
        {
            var correctionLengthSquared = correction.X * correction.X + correction.Y * correction.Y;
            if (correctionLengthSquared > CollisionWakeThresholdSquared) WakeUp();
        }

        Owner.Move(correction);
    }

    internal void EndPhysicsStep(double dt)
    {
        if (BodyType != CollisionBodyType2D.Dynamic || !AllowSleep || !Owner.ActiveInHierarchy)
        {
            IsSleeping = false;
            _sleepTimer = 0.0;
            return;
        }

        var speedSquared = _velocity.X * _velocity.X + _velocity.Y * _velocity.Y;
        if (speedSquared > SleepVelocityThreshold * SleepVelocityThreshold)
        {
            IsSleeping = false;
            _sleepTimer = 0.0;
            return;
        }

        _sleepTimer += dt;
        if (_sleepTimer >= TimeToSleep) Sleep();
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

    private void ClampVelocity()
    {
        if (double.IsPositiveInfinity(MaximumSpeed)) return;

        var speedSquared = _velocity.X * _velocity.X + _velocity.Y * _velocity.Y;
        var maximumSpeedSquared = MaximumSpeed * MaximumSpeed;
        if (speedSquared <= maximumSpeedSquared) return;

        _velocity *= MaximumSpeed / Math.Sqrt(speedSquared);
    }
}
