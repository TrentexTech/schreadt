using Schreadt_Engine.Component;
using Silk.NET.Maths;

namespace Schreadt_Engine.Collision;

/// <summary>
/// Keeps two local anchor points coincident while allowing relative rotation, or
/// pins one rigid body to a fixed world-space anchor.
/// </summary>
public sealed class RevoluteJoint2D : GameComponent
{
    private readonly bool _deriveLocalAnchor;
    private RigidBody2D? _body;
    private Vector2D<double> _localAnchor;
    private Vector2D<double> _worldAnchor;
    private double _breakImpulseThreshold = double.PositiveInfinity;

    /// <summary>
    /// Creates a body-to-body joint using local-space anchors on both bodies. Both bodies
    /// must belong to the same scene when the joint component is registered.
    /// </summary>
    public RevoluteJoint2D(
        RigidBody2D connectedBody,
        Vector2D<double> localAnchor,
        Vector2D<double> connectedLocalAnchor)
    {
        ArgumentNullException.ThrowIfNull(connectedBody);
        EnsureFinite(localAnchor, nameof(localAnchor));
        EnsureFinite(connectedLocalAnchor, nameof(connectedLocalAnchor));
        ConnectedBody = connectedBody;
        _localAnchor = localAnchor;
        ConnectedLocalAnchor = connectedLocalAnchor;
    }

    /// <summary>
    /// Creates a joint to a fixed world anchor. The owner's local anchor is derived
    /// from its transform when the component is attached.
    /// </summary>
    public RevoluteJoint2D(Vector2D<double> worldAnchor)
    {
        EnsureFinite(worldAnchor, nameof(worldAnchor));
        _worldAnchor = worldAnchor;
        _deriveLocalAnchor = true;
    }

    /// <summary>Creates a joint from an explicit local anchor to a fixed world anchor.</summary>
    public RevoluteJoint2D(Vector2D<double> localAnchor, Vector2D<double> worldAnchor)
    {
        EnsureFinite(localAnchor, nameof(localAnchor));
        EnsureFinite(worldAnchor, nameof(worldAnchor));
        _localAnchor = localAnchor;
        _worldAnchor = worldAnchor;
    }

    public RigidBody2D Body => _body
        ?? throw new InvalidOperationException("The revolute joint is not attached to a rigid body.");

    public RigidBody2D? ConnectedBody { get; }

    public Vector2D<double> LocalAnchor => _localAnchor;

    public Vector2D<double> ConnectedLocalAnchor { get; }

    public Vector2D<double> WorldAnchor => _worldAnchor;

    public bool Enabled { get; set; } = true;

    public bool LimitsEnabled { get; private set; }

    public double LowerAngle { get; private set; }

    public double UpperAngle { get; private set; }

    /// <summary>
    /// Maximum accumulated linear or angular solver impulse before the joint breaks.
    /// Positive infinity, the default, disables breaking.
    /// </summary>
    public double BreakImpulseThreshold
    {
        get => _breakImpulseThreshold;
        set
        {
            if ((!double.IsFinite(value) && !double.IsPositiveInfinity(value)) || value <= 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    "The break impulse threshold must be positive or positive infinity.");
            }

            _breakImpulseThreshold = value;
        }
    }

    public bool IsBroken { get; private set; }

    public Vector2D<double> FirstWorldAnchor =>
        Body.Owner.Position + Rotate(LocalAnchor, Body.Owner.Transform.WorldRotation);

    public Vector2D<double> SecondWorldAnchor => ConnectedBody is { } connected
        ? connected.Owner.Position + Rotate(ConnectedLocalAnchor, connected.Owner.Transform.WorldRotation)
        : WorldAnchor;

    public event Action<RevoluteJoint2D>? Broken;

    public void SetLimits(double lowerAngle, double upperAngle)
    {
        if (!double.IsFinite(lowerAngle))
            throw new ArgumentOutOfRangeException(nameof(lowerAngle), "The lower angle must be finite.");
        if (!double.IsFinite(upperAngle) || upperAngle < lowerAngle)
            throw new ArgumentOutOfRangeException(nameof(upperAngle), "The upper angle must be finite and not below the lower angle.");

        LowerAngle = lowerAngle;
        UpperAngle = upperAngle;
        LimitsEnabled = true;
        if (Attached)
        {
            Body.WakeUp();
            ConnectedBody?.WakeUp();
        }
    }

    public void ClearLimits()
    {
        LimitsEnabled = false;
        if (Attached)
        {
            Body.WakeUp();
            ConnectedBody?.WakeUp();
        }
    }

    /// <summary>Repairs or re-registers an attached joint with its owner's current scene.</summary>
    public void Repair()
    {
        if (!Attached) throw new InvalidOperationException("The revolute joint must be attached before it can be repaired.");
        if (World is not null) return;

        var wasBroken = IsBroken;
        IsBroken = false;
        try
        {
            Owner.Scene?.Collisions.AddJoint(this);
        }
        catch
        {
            IsBroken = wasBroken;
            throw;
        }
    }

    internal CollisionWorld2D? World { get; set; }

    internal double ReferenceAngle { get; set; }

    internal bool CanSolveIn(CollisionWorld2D world) =>
        !IsBroken &&
        Enabled &&
        ReferenceEquals(World, world) &&
        ReferenceEquals(Body.World, world) &&
        Body.Owner.ActiveInHierarchy &&
        (ConnectedBody is null ||
         (ReferenceEquals(ConnectedBody.World, world) && ConnectedBody.Owner.ActiveInHierarchy));

    internal void MarkBroken()
    {
        if (IsBroken) return;
        IsBroken = true;
        Broken?.Invoke(this);
    }

    protected override void OnAttached()
    {
        _body = Owner.GetComponent<RigidBody2D>() ?? Owner.AddComponent(new RigidBody2D());
        if (ReferenceEquals(_body, ConnectedBody))
            throw new InvalidOperationException("A revolute joint cannot connect a rigid body to itself.");

        _body.AttachJoint(this);
        ConnectedBody?.AttachJoint(this);

        if (_deriveLocalAnchor)
        {
            _localAnchor = Rotate(
                WorldAnchor - Owner.Position,
                -Owner.Transform.WorldRotation);
        }
    }

    protected override void OnDetached()
    {
        _body?.DetachJoint(this);
        ConnectedBody?.DetachJoint(this);
        _body = null;
        World = null;
    }

    private static Vector2D<double> Rotate(Vector2D<double> value, double radians)
    {
        var cosine = Math.Cos(radians);
        var sine = Math.Sin(radians);
        return new Vector2D<double>(
            value.X * cosine - value.Y * sine,
            value.X * sine + value.Y * cosine);
    }

    private static void EnsureFinite(Vector2D<double> value, string parameterName)
    {
        if (!double.IsFinite(value.X) || !double.IsFinite(value.Y))
            throw new ArgumentOutOfRangeException(parameterName, "Anchor coordinates must be finite.");
    }
}
