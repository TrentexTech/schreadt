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
    private CollisionBodyType2D _bodyType;
    private Vector2D<double> _velocity;
    private Vector2D<double> _accumulatedForce;
    private double _angularVelocity;
    private double _accumulatedTorque;
    private bool _useGravity = true;
    private bool _allowSleep = true;
    private bool _fixedRotation;
    private double _gravityScale = 1.0;
    private double _mass = 1.0;
    private double _momentOfInertia = 1.0;
    private double _restitution;
    private double _friction = 0.5;
    private double _linearDamping;
    private double _angularDamping;
    private double _maximumSpeed = double.PositiveInfinity;
    private double _maximumAngularSpeed = double.PositiveInfinity;
    private double _sleepVelocityThreshold = 0.02;
    private double _sleepAngularVelocityThreshold = 0.02;
    private double _timeToSleep = 0.5;
    private double _sleepTimer;
    private readonly HashSet<RevoluteJoint2D> _joints = [];

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
            if (value != CollisionBodyType2D.Dynamic)
            {
                _accumulatedForce = Vector2D<double>.Zero;
                _accumulatedTorque = 0.0;
            }
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

    /// <summary>Counter-clockwise angular velocity in radians per second.</summary>
    public double AngularVelocity
    {
        get => _angularVelocity;
        set
        {
            EnsureFinite(value, nameof(value), "Angular velocity must be finite.");
            _angularVelocity = FixedRotation ? 0.0 : value;
            ClampAngularVelocity();
            WakeUp();
        }
    }

    public double AccumulatedTorque => _accumulatedTorque;

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

    /// <summary>
    /// Explicit moment of inertia around the owner's origin. Compound bodies must choose this value deliberately.
    /// </summary>
    public double MomentOfInertia
    {
        get => _momentOfInertia;
        set
        {
            if (!double.IsFinite(value) || value <= 0.0)
                throw new ArgumentOutOfRangeException(nameof(value), "Moment of inertia must be finite and greater than zero.");

            _momentOfInertia = value;
            WakeUp();
        }
    }

    public bool FixedRotation
    {
        get => _fixedRotation;
        set
        {
            if (_fixedRotation == value) return;
            _fixedRotation = value;
            if (value)
            {
                _angularVelocity = 0.0;
                _accumulatedTorque = 0.0;
            }

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

    public double AngularDamping
    {
        get => _angularDamping;
        set
        {
            if (!double.IsFinite(value) || value < 0.0)
                throw new ArgumentOutOfRangeException(nameof(value), "Angular damping must be finite and non-negative.");

            _angularDamping = value;
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

    public double MaximumAngularSpeed
    {
        get => _maximumAngularSpeed;
        set
        {
            if ((!double.IsFinite(value) && !double.IsPositiveInfinity(value)) || value <= 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    "Maximum angular speed must be positive or positive infinity.");
            }

            _maximumAngularSpeed = value;
            ClampAngularVelocity();
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

    public double SleepAngularVelocityThreshold
    {
        get => _sleepAngularVelocityThreshold;
        set
        {
            if (!double.IsFinite(value) || value < 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    "Sleep angular velocity threshold must be finite and non-negative.");
            }

            _sleepAngularVelocityThreshold = value;
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

    internal double InverseMomentOfInertia =>
        BodyType == CollisionBodyType2D.Dynamic && !FixedRotation ? 1.0 / MomentOfInertia : 0.0;

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

    public void AddImpulseAtPoint(Vector2D<double> impulse, Vector2D<double> worldPoint)
    {
        EnsureFinite(impulse, nameof(impulse));
        EnsureFinite(worldPoint, nameof(worldPoint));
        if (BodyType != CollisionBodyType2D.Dynamic) return;

        _velocity += impulse * InverseMass;
        if (!FixedRotation)
            _angularVelocity += Cross(worldPoint - Owner.Position, impulse) * InverseMomentOfInertia;
        ClampVelocity();
        ClampAngularVelocity();
        WakeUp();
    }

    public void AddTorque(double torque)
    {
        EnsureFinite(torque, nameof(torque), "Torque must be finite.");
        if (BodyType != CollisionBodyType2D.Dynamic || FixedRotation) return;

        _accumulatedTorque += torque;
        WakeUp();
    }

    public void AddAngularImpulse(double impulse)
    {
        EnsureFinite(impulse, nameof(impulse), "Angular impulse must be finite.");
        if (BodyType != CollisionBodyType2D.Dynamic || FixedRotation) return;

        _angularVelocity += impulse * InverseMomentOfInertia;
        ClampAngularVelocity();
        WakeUp();
    }

    public void ClearForces()
    {
        _accumulatedForce = Vector2D<double>.Zero;
        _accumulatedTorque = 0.0;
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
        _angularVelocity = 0.0;
        _accumulatedForce = Vector2D<double>.Zero;
        _accumulatedTorque = 0.0;
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
            if (!FixedRotation)
                _angularVelocity += _accumulatedTorque * InverseMomentOfInertia * dt;

            if (LinearDamping > 0)
            {
                _velocity *= Math.Exp(-LinearDamping * dt);
            }

            if (AngularDamping > 0.0)
                _angularVelocity *= Math.Exp(-AngularDamping * dt);

            ClampVelocity();
            ClampAngularVelocity();
        }

        Owner.Move(_velocity * dt);
        if (!FixedRotation && _angularVelocity != 0.0)
            Owner.Transform.SetWorldRotation(Owner.Transform.WorldRotation + _angularVelocity * dt);
        ClearForces();
    }

    internal void ApplyCollisionImpulseAtPoint(Vector2D<double> impulse, Vector2D<double> worldPoint)
    {
        if (BodyType != CollisionBodyType2D.Dynamic || IsSleeping) return;

        var angularImpulse = FixedRotation ? 0.0 : Cross(worldPoint - Owner.Position, impulse);
        _velocity += impulse * InverseMass;
        _angularVelocity += angularImpulse * InverseMomentOfInertia;
        ClampVelocity();
        ClampAngularVelocity();
    }

    internal void ApplyCollisionAngularImpulse(double impulse)
    {
        if (BodyType != CollisionBodyType2D.Dynamic || IsSleeping || FixedRotation) return;

        _angularVelocity += impulse * InverseMomentOfInertia;
        ClampAngularVelocity();
    }

    internal Vector2D<double> GetVelocityAtPoint(Vector2D<double> worldPoint)
    {
        var radius = worldPoint - Owner.Position;
        return _velocity + new Vector2D<double>(-_angularVelocity * radius.Y, _angularVelocity * radius.X);
    }

    internal void ApplyPositionImpulseAtPoint(Vector2D<double> impulse, Vector2D<double> worldPoint)
    {
        if (BodyType != CollisionBodyType2D.Dynamic || IsSleeping) return;

        var radius = worldPoint - Owner.Position;
        var angularCorrection = FixedRotation
            ? 0.0
            : Cross(radius, impulse) * InverseMomentOfInertia;
        Owner.Move(impulse * InverseMass);
        if (angularCorrection != 0.0)
            Owner.Transform.SetWorldRotation(Owner.Transform.WorldRotation + angularCorrection);
    }

    internal void ApplyPositionAngularImpulse(double impulse)
    {
        if (BodyType != CollisionBodyType2D.Dynamic || IsSleeping || FixedRotation) return;

        Owner.Transform.SetWorldRotation(
            Owner.Transform.WorldRotation + impulse * InverseMomentOfInertia);
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
        if (speedSquared > SleepVelocityThreshold * SleepVelocityThreshold ||
            Math.Abs(_angularVelocity) > SleepAngularVelocityThreshold)
        {
            IsSleeping = false;
            _sleepTimer = 0.0;
            return;
        }

        _sleepTimer += dt;
        if (_sleepTimer >= TimeToSleep) Sleep();
    }

    internal void AttachJoint(RevoluteJoint2D joint) => _joints.Add(joint);

    internal void DetachJoint(RevoluteJoint2D joint) => _joints.Remove(joint);

    protected override void OnAttached()
    {
        if (Owner.GetComponent<RigidBody2D>() is not null)
            throw new InvalidOperationException("A game object can only have one rigid body.");
    }

    internal override void ValidateCanDetach()
    {
        if (Owner.GetComponents<Collider2D>().Count > 0)
            throw new InvalidOperationException("Remove the game object's colliders before removing its rigid body.");
        if (_joints.Count > 0)
            throw new InvalidOperationException("Remove the rigid body's attached joints before removing its rigid body.");
    }

    private static void EnsureFinite(Vector2D<double> value, string parameterName)
    {
        if (!double.IsFinite(value.X) || !double.IsFinite(value.Y))
            throw new ArgumentOutOfRangeException(parameterName, "Vector components must be finite.");
    }

    private static void EnsureFinite(double value, string parameterName, string message)
    {
        if (!double.IsFinite(value)) throw new ArgumentOutOfRangeException(parameterName, message);
    }

    private static double Cross(Vector2D<double> first, Vector2D<double> second) =>
        first.X * second.Y - first.Y * second.X;

    private void ClampVelocity()
    {
        if (double.IsPositiveInfinity(MaximumSpeed)) return;

        var speedSquared = _velocity.X * _velocity.X + _velocity.Y * _velocity.Y;
        var maximumSpeedSquared = MaximumSpeed * MaximumSpeed;
        if (speedSquared <= maximumSpeedSquared) return;

        _velocity *= MaximumSpeed / Math.Sqrt(speedSquared);
    }

    private void ClampAngularVelocity()
    {
        if (FixedRotation)
        {
            _angularVelocity = 0.0;
            return;
        }

        if (double.IsPositiveInfinity(MaximumAngularSpeed) ||
            Math.Abs(_angularVelocity) <= MaximumAngularSpeed)
        {
            return;
        }

        _angularVelocity = Math.CopySign(MaximumAngularSpeed, _angularVelocity);
    }
}
