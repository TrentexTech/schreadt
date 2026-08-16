using Schreadt_Engine.Animation.Tweening;
using Schreadt_Engine.Collision;
using Schreadt_Engine.Component;
using Schreadt_Engine.Component.PreFab;
using Schreadt_Engine.Core;
using Silk.NET.Maths;

namespace Example_Game.Logic.scenes;

internal sealed class FoundryCrate : Rectangle2D
{
    private readonly Vector2D<double> _spawnPosition;
    private readonly double _spawnRotation;

    internal RigidBody2D Body { get; }
    internal OrientedBoxCollider2D Collider { get; }

    internal FoundryCrate(
        Vector2D<double> spawnPosition,
        double mass,
        double friction,
        Vector4D<float> color,
        Vector2D<double>? size = null,
        double rotation = 0.0)
    {
        _spawnPosition = spawnPosition;
        _spawnRotation = rotation;
        Position = spawnPosition;
        RotationRadians = rotation;
        Size = size ?? new Vector2D<double>(0.62, 0.46);
        Color = color;
        RenderLayer = 21;
        Body = AddComponent(new RigidBody2D
        {
            BodyType = CollisionBodyType2D.Dynamic,
            Mass = mass,
            MomentOfInertia = PhysicsInertia2D.ForSolidBox(mass, Size),
            Friction = friction,
            Restitution = 0.04,
            LinearDamping = 0.7,
            AngularDamping = 1.0,
            MaximumSpeed = 6.0,
            MaximumAngularSpeed = 8.0,
            SleepVelocityThreshold = 0.08,
            SleepAngularVelocityThreshold = 0.16,
            TimeToSleep = 0.35
        });
        Collider = AddComponent(new OrientedBoxCollider2D(Size)
        {
            CollisionLayer = ExampleCollisionLayers.Mechanic,
            CollisionMask = ExampleCollisionLayers.PushableMask
        });
    }

    internal void Reset()
    {
        Position = _spawnPosition;
        RotationRadians = _spawnRotation;
        Body.Velocity = Vector2D<double>.Zero;
        Body.AngularVelocity = 0.0;
        Body.ClearForces();
        Body.WakeUp();
    }

    protected override void OnRender(IRenderContext2D renderer)
    {
        base.OnRender(renderer);
        renderer.DrawRectangle(
            Position,
            Size * 0.66,
            new Vector4D<float>(0.18f, 0.09f, 0.035f, 0.82f),
            RotationRadians);
    }
}

internal abstract class FoundryInteractable : Rectangle2D, IInteractable2D
{
    protected FoundryInteractable(Vector2D<double> size, Vector4D<float> color)
    {
        Size = size;
        Color = color;
        RenderLayer = 24;
        AddComponent(new CircleCollider2D(0.4)
        {
            IsTrigger = true,
            CollisionLayer = ExampleCollisionLayers.Interactable,
            CollisionMask = CollisionLayerMask2D.None
        });
    }

    public abstract string InteractionPrompt { get; }
    public abstract bool CanInteract(PlayerAvatar player);
    public abstract void Interact(PlayerAvatar player);
}

internal sealed class FoundryResetStation : FoundryInteractable
{
    private readonly string _mechanism;
    private readonly Action _reset;

    internal int ResetCount { get; private set; }
    public override string InteractionPrompt => $"E: RESET {_mechanism}";

    internal FoundryResetStation(string mechanism, Action reset)
        : base(new Vector2D<double>(0.34, 0.42), new Vector4D<float>(0.16f, 0.62f, 0.88f, 1.0f))
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mechanism);
        ArgumentNullException.ThrowIfNull(reset);
        _mechanism = mechanism;
        _reset = reset;
    }

    public override bool CanInteract(PlayerAvatar player) => true;

    public override void Interact(PlayerAvatar player)
    {
        ResetCount++;
        _reset();
    }
}

internal sealed class FoundryLatch : FoundryInteractable
{
    private readonly Action _release;

    internal bool Released { get; private set; }
    public override string InteractionPrompt => "E: RELEASE HAMMER";

    internal FoundryLatch(Action release)
        : base(new Vector2D<double>(0.2, 0.58), new Vector4D<float>(0.82f, 0.26f, 0.12f, 1.0f))
    {
        ArgumentNullException.ThrowIfNull(release);
        _release = release;
    }

    public override bool CanInteract(PlayerAvatar player) => !Released;

    public override void Interact(PlayerAvatar player)
    {
        if (Released) return;
        Released = true;
        RotationRadians = -0.48;
        Color = new Vector4D<float>(0.3f, 0.9f, 0.45f, 1.0f);
        _release();
    }

    internal void Reset()
    {
        Released = false;
        RotationRadians = 0.0;
        Color = new Vector4D<float>(0.82f, 0.26f, 0.12f, 1.0f);
    }
}

internal sealed class FoundryIgnitionLever : FoundryInteractable
{
    private readonly Func<bool> _canIgnite;
    private readonly Action _ignite;

    internal bool Ignited { get; private set; }
    internal bool Rejected { get; private set; }
    public override string InteractionPrompt => Rejected
        ? "BLOCK THE SENSOR WITH THE CRATE"
        : "E: IGNITE THE FOUNDRY";

    internal FoundryIgnitionLever(Func<bool> canIgnite, Action ignite)
        : base(new Vector2D<double>(0.2, 0.62), new Vector4D<float>(0.9f, 0.28f, 0.1f, 1.0f))
    {
        ArgumentNullException.ThrowIfNull(canIgnite);
        ArgumentNullException.ThrowIfNull(ignite);
        _canIgnite = canIgnite;
        _ignite = ignite;
    }

    public override bool CanInteract(PlayerAvatar player) => !Ignited;

    public override void Interact(PlayerAvatar player)
    {
        if (Ignited) return;
        if (!_canIgnite())
        {
            Rejected = true;
            return;
        }

        Ignited = true;
        Rejected = false;
        RotationRadians = -0.52;
        Color = new Vector4D<float>(1.0f, 0.78f, 0.18f, 1.0f);
        _ignite();
    }
}

internal sealed class FoundryDoor : Rectangle2D
{
    private readonly Vector2D<double> _closedPosition;
    private readonly TweenPlayer _tweens;

    internal OrientedBoxCollider2D Collider { get; }
    internal bool IsOpen { get; private set; }

    internal FoundryDoor(Vector2D<double> position)
    {
        _closedPosition = position;
        Position = position;
        Size = new Vector2D<double>(0.3, 2.6);
        Color = new Vector4D<float>(0.34f, 0.2f, 0.17f, 1.0f);
        RenderLayer = 16;
        Collider = AddComponent(new OrientedBoxCollider2D(Size)
        {
            CollisionLayer = ExampleCollisionLayers.World,
            CollisionMask = ExampleCollisionLayers.WorldMask
        });
        _tweens = AddComponent(new TweenPlayer());
    }

    internal void Open()
    {
        if (IsOpen) return;
        IsOpen = true;
        Collider.Enabled = false;
        var lift = Tweens.To(
            () => Position,
            value => Position = value,
            _closedPosition + new Vector2D<double>(0.0, 2.45),
            0.5);
        lift.Easing = TweenEasings.CubicOut;
        _tweens.Play(lift);
    }

    internal void Reset()
    {
        _tweens.Clear();
        IsOpen = false;
        Position = _closedPosition;
        Collider.Enabled = true;
    }
}

internal sealed class FoundryPressurePlate : Rectangle2D
{
    private readonly FoundryCrate _target;

    internal bool Pressed { get; private set; }

    internal FoundryPressurePlate(Vector2D<double> position, FoundryCrate target)
    {
        _target = target;
        Position = position;
        Size = new Vector2D<double>(0.82, 0.1);
        Color = new Vector4D<float>(0.86f, 0.3f, 0.12f, 1.0f);
        RenderLayer = 18;
    }

    internal bool Evaluate()
    {
        var offset = _target.Position - Position;
        Pressed = Math.Abs(offset.X) <= 0.5 && offset.Y is >= -0.1 and <= 0.55;
        Color = Pressed
            ? new Vector4D<float>(0.24f, 0.95f, 0.45f, 1.0f)
            : new Vector4D<float>(0.86f, 0.3f, 0.12f, 1.0f);
        Transform.LocalScale = Pressed
            ? new Vector2D<double>(1.0, 0.45)
            : Vector2D<double>.One;
        return Pressed;
    }
}

internal sealed class FoundrySeesaw : Rectangle2D
{
    private readonly Vector2D<double> _spawnPosition;

    internal RigidBody2D Body { get; }
    internal OrientedBoxCollider2D Collider { get; }
    internal RevoluteJoint2D Joint { get; }

    internal FoundrySeesaw(Vector2D<double> size, Vector2D<double> worldAnchor)
    {
        _spawnPosition = worldAnchor;
        Position = worldAnchor;
        Size = size;
        Color = new Vector4D<float>(0.16f, 0.74f, 0.7f, 1.0f);
        RenderLayer = 12;
        const double mass = 1.6;
        Body = AddComponent(new RigidBody2D
        {
            BodyType = CollisionBodyType2D.Dynamic,
            Mass = mass,
            MomentOfInertia = PhysicsInertia2D.ForSolidBox(mass, size),
            Friction = 0.9,
            LinearDamping = 0.35,
            AngularDamping = 1.0,
            MaximumAngularSpeed = 4.0,
            AllowSleep = false
        });
        Collider = AddComponent(new OrientedBoxCollider2D(size)
        {
            CollisionLayer = ExampleCollisionLayers.World,
            CollisionMask = ExampleCollisionLayers.WorldMask
        });
        Joint = AddComponent(new RevoluteJoint2D(Vector2D<double>.Zero, worldAnchor));
        Joint.SetLimits(-0.34, 0.34);
        AddChild(new Circle
        {
            Radius = 0.1,
            Color = new Vector4D<float>(1.0f, 0.68f, 0.12f, 1.0f),
            RenderLayer = 13
        });
    }

    internal void Reset()
    {
        Position = _spawnPosition;
        RotationRadians = 0.0;
        Body.Velocity = Vector2D<double>.Zero;
        Body.AngularVelocity = 0.0;
        Body.ClearForces();
        Body.WakeUp();
    }
}

internal sealed class FoundryRamp : Rectangle2D
{
    internal OrientedBoxCollider2D Collider { get; }

    internal FoundryRamp(Vector2D<double> bottomSurface, Vector2D<double> topSurface)
    {
        var surface = topSurface - bottomSurface;
        var length = Math.Sqrt(surface.X * surface.X + surface.Y * surface.Y);
        if (!double.IsFinite(length) || length <= 0.0)
            throw new ArgumentException("Ramp endpoints must define a finite positive length.", nameof(topSurface));

        const double thickness = 0.12;
        var rotation = Math.Atan2(surface.Y, surface.X);
        var upwardNormal = new Vector2D<double>(-Math.Sin(rotation), Math.Cos(rotation));
        Position = (bottomSurface + topSurface) * 0.5 - upwardNormal * (thickness * 0.5);
        RotationRadians = rotation;
        Size = new Vector2D<double>(length, thickness);
        Color = new Vector4D<float>(0.32f, 0.23f, 0.29f, 1.0f);
        RenderLayer = 12;
        Collider = AddComponent(new OrientedBoxCollider2D(Size)
        {
            CollisionLayer = ExampleCollisionLayers.World,
            CollisionMask = ExampleCollisionLayers.WorldMask
        });
    }
}

internal sealed class FoundryPressLinkage : GameObject
{
    private readonly Rectangle2D _ram;
    private readonly TweenPlayer _tweens;

    internal FoundryPressLinkage(Vector2D<double> position)
    {
        Position = position;
        _tweens = AddComponent(new TweenPlayer());
        AddChild(new Rectangle2D
        {
            Size = new Vector2D<double>(1.5, 0.18),
            Color = new Vector4D<float>(0.48f, 0.3f, 0.2f, 1.0f),
            RenderLayer = 14,
            Transform = { LocalPosition = new Vector2D<double>(0.0, 0.55) }
        });
        _ram = new Rectangle2D
        {
            Size = new Vector2D<double>(0.28, 0.75),
            Color = new Vector4D<float>(0.92f, 0.5f, 0.12f, 1.0f),
            RenderLayer = 15,
            Transform = { LocalPosition = new Vector2D<double>(0.0, 0.15) }
        };
        AddChild(_ram);
    }

    internal void Engage()
    {
        var descend = Tweens.To(
            () => _ram.Transform.LocalPosition,
            value => _ram.Transform.LocalPosition = value,
            new Vector2D<double>(0.0, -0.12),
            0.35);
        descend.Easing = TweenEasings.QuadraticInOut;
        _tweens.Play(descend);
    }
}

internal sealed class FoundryPendulumHammer : Rectangle2D
{
    private readonly Vector2D<double> _spawnPosition;
    private readonly double _spawnRotation;

    internal RigidBody2D Body { get; }
    internal RevoluteJoint2D Joint { get; }
    internal bool Released => Body.BodyType == CollisionBodyType2D.Dynamic;

    internal FoundryPendulumHammer(Vector2D<double> position, Vector2D<double> anchor, double rotation)
    {
        _spawnPosition = position;
        _spawnRotation = rotation;
        Position = position;
        RotationRadians = rotation;
        Size = new Vector2D<double>(0.34, 1.7);
        Color = new Vector4D<float>(0.5f, 0.31f, 0.2f, 1.0f);
        RenderLayer = 20;
        const double mass = 2.5;
        Body = AddComponent(new RigidBody2D
        {
            BodyType = CollisionBodyType2D.Static,
            Mass = mass,
            MomentOfInertia = PhysicsInertia2D.ForSolidBox(mass, Size),
            Friction = 0.8,
            AngularDamping = 0.08,
            MaximumAngularSpeed = 5.0,
            AllowSleep = false
        });
        AddComponent(new OrientedBoxCollider2D(Size)
        {
            CollisionLayer = ExampleCollisionLayers.Mechanic,
            CollisionMask = ExampleCollisionLayers.PushableMask
        });
        AddComponent(new OrientedBoxCollider2D(new Vector2D<double>(0.8, 0.42))
        {
            Offset = new Vector2D<double>(0.0, -Size.Y * 0.5),
            CollisionLayer = ExampleCollisionLayers.Mechanic,
            CollisionMask = ExampleCollisionLayers.PushableMask
        });
        Joint = AddComponent(new RevoluteJoint2D(new Vector2D<double>(0.0, Size.Y * 0.5), anchor));
        AddChild(new Circle
        {
            Radius = 0.12,
            Color = new Vector4D<float>(1.0f, 0.68f, 0.12f, 1.0f),
            RenderLayer = 22,
            Transform = { LocalPosition = new Vector2D<double>(0.0, Size.Y * 0.5) }
        });
        AddChild(new Rectangle2D
        {
            Size = new Vector2D<double>(0.8, 0.42),
            Color = new Vector4D<float>(0.8f, 0.24f, 0.1f, 1.0f),
            RenderLayer = 21,
            Transform = { LocalPosition = new Vector2D<double>(0.0, -Size.Y * 0.5) }
        });
    }

    internal void Release()
    {
        Body.BodyType = CollisionBodyType2D.Dynamic;
        Body.WakeUp();
    }

    internal void Reset()
    {
        Body.BodyType = CollisionBodyType2D.Static;
        Position = _spawnPosition;
        RotationRadians = _spawnRotation;
        Body.Velocity = Vector2D<double>.Zero;
        Body.AngularVelocity = 0.0;
        Body.ClearForces();
    }
}

internal sealed class FoundryRotor : GameObject
{
    private readonly RigidBody2D _body;

    internal double PoweredAngularVelocity { get; set; } = 0.72;
    internal bool SafetyStopped { get; set; }
    internal IReadOnlyList<OrientedBoxCollider2D> Arms { get; }

    internal FoundryRotor(Vector2D<double> position)
    {
        Position = position;
        _body = AddComponent(new RigidBody2D { BodyType = CollisionBodyType2D.Kinematic });
        var horizontal = AddComponent(new OrientedBoxCollider2D(new Vector2D<double>(3.0, 0.24))
        {
            CollisionLayer = ExampleCollisionLayers.Mechanic,
            CollisionMask = ExampleCollisionLayers.PushableMask
        });
        var vertical = AddComponent(new OrientedBoxCollider2D(new Vector2D<double>(0.24, 3.0))
        {
            CollisionLayer = ExampleCollisionLayers.Mechanic,
            CollisionMask = ExampleCollisionLayers.PushableMask
        });
        Arms = [horizontal, vertical];
        AddChild(CreateVisualArm(new Vector2D<double>(3.0, 0.24)));
        AddChild(CreateVisualArm(new Vector2D<double>(0.24, 3.0)));
        AddChild(new Circle
        {
            Radius = 0.22,
            Color = new Vector4D<float>(1.0f, 0.66f, 0.12f, 1.0f),
            RenderLayer = 22
        });
    }

    internal double AngularVelocity => _body.AngularVelocity;

    protected override void OnFixedUpdate(double dt)
    {
        base.OnFixedUpdate(dt);
        _body.AngularVelocity = SafetyStopped ? 0.0 : PoweredAngularVelocity;
    }

    private static Rectangle2D CreateVisualArm(Vector2D<double> size) => new()
    {
        Size = size,
        Color = new Vector4D<float>(0.52f, 0.28f, 0.62f, 1.0f),
        RenderLayer = 20
    };
}

internal sealed class FoundrySafetySensor : Actor
{
    private static readonly CollisionQueryFilter2D SensorFilter = new(
        CollisionLayerMask2D.FromLayers(ExampleCollisionLayers.Player),
        includeTriggers: false);
    private readonly FoundryRotor _rotor;
    private const double MaximumBeamDistance = 3.0;
    private double _beamDistance = MaximumBeamDistance;

    internal bool Obstructed { get; private set; }

    internal FoundrySafetySensor(Vector2D<double> position, FoundryRotor rotor)
    {
        Position = position;
        _rotor = rotor;
        RenderLayer = 23;
    }

    protected override void OnFixedUpdate(double dt)
    {
        base.OnFixedUpdate(dt);
        var hit = default(RaycastHit2D);
        Obstructed = Scene is not null && Scene.Collisions.Raycast(
            Position,
            new Vector2D<double>(0.0, -1.0),
            MaximumBeamDistance,
            out hit,
            SensorFilter);
        _beamDistance = Obstructed ? hit.Distance : MaximumBeamDistance;
        _rotor.SafetyStopped = Obstructed;
    }

    protected override void OnRender(IRenderContext2D renderer)
    {
        renderer.DrawCircle(Position, 0.13, new Vector4D<float>(1.0f, 0.74f, 0.12f, 1.0f));
        if (_beamDistance <= 0.0) return;
        renderer.DrawRectangle(
            Position + new Vector2D<double>(0.0, -_beamDistance * 0.5),
            new Vector2D<double>(0.025, _beamDistance),
            Obstructed
                ? new Vector4D<float>(0.28f, 1.0f, 0.5f, 0.9f)
                : new Vector4D<float>(1.0f, 0.18f, 0.12f, 0.9f));
    }
}

internal sealed class FoundryCrateSensor : Actor
{
    private readonly FoundryCrate _crate;
    private double _beamDistance = 2.6;

    internal bool BlockedByCrate { get; private set; }

    internal FoundryCrateSensor(Vector2D<double> position, FoundryCrate crate)
    {
        Position = position;
        _crate = crate;
        RenderLayer = 23;
    }

    protected override void OnFixedUpdate(double dt)
    {
        base.OnFixedUpdate(dt);
        var filter = new CollisionQueryFilter2D(
            ExampleCollisionLayers.MechanicOnlyMask,
            includeTriggers: false,
            predicate: collider => ReferenceEquals(collider.Owner, _crate));
        var hit = default(RaycastHit2D);
        BlockedByCrate = Scene is not null && Scene.Collisions.Raycast(
            Position,
            new Vector2D<double>(1.0, 0.0),
            2.6,
            out hit,
            filter);
        _beamDistance = BlockedByCrate ? hit.Distance : 2.6;
    }

    protected override void OnRender(IRenderContext2D renderer)
    {
        renderer.DrawCircle(Position, 0.14, new Vector4D<float>(1.0f, 0.68f, 0.12f, 1.0f));
        if (_beamDistance <= 0.0) return;
        renderer.DrawRectangle(
            Position + new Vector2D<double>(_beamDistance * 0.5, 0.0),
            new Vector2D<double>(_beamDistance, 0.025),
            BlockedByCrate
                ? new Vector4D<float>(0.25f, 1.0f, 0.46f, 0.9f)
                : new Vector4D<float>(1.0f, 0.16f, 0.1f, 0.9f));
    }
}
