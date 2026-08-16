using Schreadt_Engine.Collision;
using Schreadt_Engine.Component.PreFab;
using Schreadt_Engine.Core;
using Silk.NET.Maths;

namespace Example_Game.Logic.scenes;

/// <summary>
/// Temporary gameplay scaffold for exercising transform-oriented collision before
/// the planned Kinetic Foundry level replaces it.
/// </summary>
internal sealed class Scene5 : PlatformerLevelLogic
{
    internal Scene5(IInputState input)
        : base(input, 6, "ORIENTED COLLIDER LAB", null)
    {
    }

    protected override Vector2D<double> SpawnPoint => new(0.0, -1.05);

    protected override string HudNote => "PROVISIONAL: ROTATION, STACKS, REVOLUTE SEESAW";

    protected override void BuildLevel()
    {
        AddBoundaryWalls();
        AddPlatform(1.0, -1.85, 4.0, 0.6);

        AddOrientedPlatform(3.75, -1.2, 2.2, 0.28, 0.34);
        AddPlatform(5.35, -0.7, 1.3, 0.28);

        Scene.AddChild(new ProvisionalRotatingBeam(
            new Vector2D<double>(2.0, 0.26),
            minimumRotation: -0.28,
            maximumRotation: 0.28,
            cycleDuration: 3.2)
        {
            Position = new Vector2D<double>(7.0, -0.15)
        });

        AddPlatform(8.7, 0.05, 1.25, 0.28);
        AddOrientedPlatform(10.35, -0.55, 3.0, 0.28, -0.28);
        AddPlatform(14.0, -1.85, 6.0, 0.6);

        Scene.AddChild(new ProvisionalInspectableSign
        {
            Position = new Vector2D<double>(1.8, -1.18)
        });

        Scene.AddChild(new ProvisionalOrientedCrate(0.35)
        {
            Position = new Vector2D<double>(9.55, 0.95)
        });

        var seesaw = new ProvisionalSeesaw(
            new Vector2D<double>(2.5, 0.18),
            new Vector2D<double>(11.9, -0.92))
        {
            Position = new Vector2D<double>(11.9, -0.92)
        };
        Scene.AddChild(seesaw);
        var seesawCrate = new ProvisionalOrientedCrate(0.0)
        {
            Position = new Vector2D<double>(11.1, -0.28)
        };
        Scene.AddChild(seesawCrate);
        Scene.AddChild(new ProvisionalLever(() =>
            seesaw.Body.AddImpulseAtPoint(
                new Vector2D<double>(0.0, -1.0),
                seesaw.Position + new Vector2D<double>(-1.0, 0.0)))
        {
            Position = new Vector2D<double>(10.65, -1.18)
        });
        Scene.AddChild(new ProvisionalResetStation(() =>
            ResetSeesaw(seesaw, seesawCrate, new Vector2D<double>(11.1, -0.28)))
        {
            Position = new Vector2D<double>(13.0, -1.18)
        });
        AddStableCrateStack(14.35, -1.31);

        AddStar(3.9, -0.35);
        AddStar(7.0, 0.65);
        AddStar(10.8, 0.25);
        AddGoal(15.75, -0.95);
    }

    private void AddOrientedPlatform(double x, double y, double width, double height, double rotation)
    {
        Scene.AddChild(new ProvisionalOrientedPlatform(
            new Vector2D<double>(width, height),
            rotation)
        {
            Position = new Vector2D<double>(x, y)
        });
    }

    private void AddStableCrateStack(double x, double bottomY)
    {
        const double verticalSpacing = 0.47;
        for (var index = 0; index < 3; index++)
        {
            Scene.AddChild(new ProvisionalOrientedCrate(0.0)
            {
                Position = new Vector2D<double>(x, bottomY + index * verticalSpacing)
            });
        }
    }

    private static void ResetSeesaw(
        ProvisionalSeesaw seesaw,
        ProvisionalOrientedCrate crate,
        Vector2D<double> cratePosition)
    {
        seesaw.Position = new Vector2D<double>(11.9, -0.92);
        seesaw.RotationRadians = 0.0;
        seesaw.Body.Velocity = Vector2D<double>.Zero;
        seesaw.Body.AngularVelocity = 0.0;
        seesaw.Body.ClearForces();

        crate.Position = cratePosition;
        crate.RotationRadians = 0.0;
        crate.Body.Velocity = Vector2D<double>.Zero;
        crate.Body.AngularVelocity = 0.0;
        crate.Body.ClearForces();
    }
}

internal abstract class ProvisionalInteractable : Rectangle2D, IInteractable2D
{
    internal CircleCollider2D InteractionCollider { get; }

    protected ProvisionalInteractable(Vector2D<double> size, Vector4D<float> color)
    {
        Size = size;
        Color = color;
        RenderLayer = 24;
        InteractionCollider = AddComponent(new CircleCollider2D(0.38)
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

internal sealed class ProvisionalLever : ProvisionalInteractable
{
    private readonly Action _activate;

    internal bool Activated { get; private set; }

    public override string InteractionPrompt => "E: PULL RELEASE LEVER";

    internal ProvisionalLever(Action activate)
        : base(new Vector2D<double>(0.18, 0.55), new Vector4D<float>(0.72f, 0.22f, 0.18f, 1.0f))
    {
        ArgumentNullException.ThrowIfNull(activate);
        _activate = activate;
    }

    public override bool CanInteract(PlayerAvatar player) => !Activated;

    public override void Interact(PlayerAvatar player)
    {
        if (!CanInteract(player)) return;
        Activated = true;
        RotationRadians = -0.45;
        Color = new Vector4D<float>(0.3f, 0.88f, 0.48f, 1.0f);
        _activate();
    }
}

internal sealed class ProvisionalResetStation : ProvisionalInteractable
{
    private readonly Action _reset;

    internal int ResetCount { get; private set; }

    public override string InteractionPrompt => "E: RESET SEESAW";

    internal ProvisionalResetStation(Action reset)
        : base(new Vector2D<double>(0.34, 0.4), new Vector4D<float>(0.2f, 0.58f, 0.86f, 1.0f))
    {
        ArgumentNullException.ThrowIfNull(reset);
        _reset = reset;
    }

    public override bool CanInteract(PlayerAvatar player) => true;

    public override void Interact(PlayerAvatar player)
    {
        ResetCount++;
        _reset();
    }
}

internal sealed class ProvisionalInspectableSign : ProvisionalInteractable
{
    internal bool Inspected { get; private set; }

    public override string InteractionPrompt => Inspected
        ? "SEESAW: LOAD EITHER ARM"
        : "E: READ FOUNDRY SIGN";

    internal ProvisionalInspectableSign()
        : base(new Vector2D<double>(0.52, 0.34), new Vector4D<float>(0.48f, 0.34f, 0.18f, 1.0f))
    {
    }

    public override bool CanInteract(PlayerAvatar player) => true;

    public override void Interact(PlayerAvatar player) => Inspected = true;
}

internal sealed class ProvisionalSeesaw : Rectangle2D
{
    internal RigidBody2D Body { get; }
    internal OrientedBoxCollider2D Collider { get; }
    internal RevoluteJoint2D Joint { get; }

    internal ProvisionalSeesaw(Vector2D<double> size, Vector2D<double> worldAnchor)
    {
        const double mass = 1.4;
        Size = size;
        Color = new Vector4D<float>(0.18f, 0.78f, 0.72f, 1.0f);
        RenderLayer = 12;
        Body = AddComponent(new RigidBody2D
        {
            BodyType = CollisionBodyType2D.Dynamic,
            Mass = mass,
            MomentOfInertia = PhysicsInertia2D.ForSolidBox(mass, size),
            Friction = 0.85,
            LinearDamping = 0.3,
            AngularDamping = 0.8,
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
            Radius = 0.09,
            Color = new Vector4D<float>(1.0f, 0.72f, 0.16f, 1.0f),
            RenderLayer = 13
        });
    }
}

internal sealed class ProvisionalOrientedPlatform : Rectangle2D
{
    internal OrientedBoxCollider2D Collider { get; }

    internal ProvisionalOrientedPlatform(Vector2D<double> size, double rotation)
    {
        Size = size;
        RotationRadians = rotation;
        Color = new Vector4D<float>(0.22f, 0.4f, 0.48f, 1.0f);
        RenderLayer = 10;
        Collider = AddComponent(new OrientedBoxCollider2D(size)
        {
            CollisionLayer = ExampleCollisionLayers.World,
            CollisionMask = ExampleCollisionLayers.WorldMask
        });

        AddChild(new Rectangle2D
        {
            Size = new Vector2D<double>(size.X, 0.07),
            Color = new Vector4D<float>(0.35f, 0.92f, 0.86f, 1.0f),
            RenderLayer = 11,
            Transform = { LocalPosition = new Vector2D<double>(0.0, size.Y * 0.5 - 0.035) }
        });
    }
}

internal sealed class ProvisionalRotatingBeam : Rectangle2D
{
    private readonly double _centerRotation;
    private readonly double _rotationAmplitude;
    private readonly double _angularFrequency;
    private readonly double _cycleDuration;
    private double _elapsed;

    internal RigidBody2D Body { get; }
    internal OrientedBoxCollider2D Collider { get; }

    internal ProvisionalRotatingBeam(
        Vector2D<double> size,
        double minimumRotation,
        double maximumRotation,
        double cycleDuration)
    {
        if (!double.IsFinite(minimumRotation))
            throw new ArgumentOutOfRangeException(nameof(minimumRotation));
        if (!double.IsFinite(maximumRotation) || maximumRotation <= minimumRotation)
            throw new ArgumentOutOfRangeException(nameof(maximumRotation));
        if (!double.IsFinite(cycleDuration) || cycleDuration <= 0.0)
            throw new ArgumentOutOfRangeException(nameof(cycleDuration));

        Size = size;
        _centerRotation = (minimumRotation + maximumRotation) * 0.5;
        _rotationAmplitude = (maximumRotation - minimumRotation) * 0.5;
        _angularFrequency = Math.Tau / cycleDuration;
        _cycleDuration = cycleDuration;
        RotationRadians = _centerRotation;
        Color = new Vector4D<float>(0.56f, 0.34f, 0.78f, 1.0f);
        RenderLayer = 12;

        Body = AddComponent(new RigidBody2D { BodyType = CollisionBodyType2D.Kinematic });
        Collider = AddComponent(new OrientedBoxCollider2D(size)
        {
            CollisionLayer = ExampleCollisionLayers.World,
            CollisionMask = ExampleCollisionLayers.WorldMask
        });
    }

    protected override void OnFixedUpdate(double dt)
    {
        base.OnFixedUpdate(dt);
        if (dt <= 0.0)
        {
            Body.AngularVelocity = 0.0;
            return;
        }

        _elapsed = (_elapsed + dt) % _cycleDuration;
        var targetRotation = _centerRotation + Math.Sin(_elapsed * _angularFrequency) * _rotationAmplitude;
        Body.AngularVelocity = (targetRotation - RotationRadians) / dt;
    }
}

internal sealed class ProvisionalOrientedCrate : Rectangle2D
{
    internal RigidBody2D Body { get; }
    internal OrientedBoxCollider2D Collider { get; }

    internal ProvisionalOrientedCrate(double rotation)
    {
        const double mass = 0.8;
        Size = new Vector2D<double>(0.62, 0.46);
        RotationRadians = rotation;
        Color = new Vector4D<float>(0.86f, 0.52f, 0.16f, 1.0f);
        RenderLayer = 21;
        Body = AddComponent(new RigidBody2D
        {
            BodyType = CollisionBodyType2D.Dynamic,
            Mass = mass,
            MomentOfInertia = PhysicsInertia2D.ForSolidBox(mass, Size),
            Friction = 0.7,
            Restitution = 0.05,
            LinearDamping = 0.8,
            AngularDamping = 1.2,
            MaximumSpeed = 4.5,
            MaximumAngularSpeed = 7.0,
            SleepVelocityThreshold = 0.08,
            SleepAngularVelocityThreshold = 0.18,
            TimeToSleep = 0.3
        });
        Collider = AddComponent(new OrientedBoxCollider2D(Size)
        {
            CollisionLayer = ExampleCollisionLayers.Mechanic,
            CollisionMask = ExampleCollisionLayers.PushableMask
        });
    }

    protected override void OnRender(IRenderContext2D renderer)
    {
        base.OnRender(renderer);
        renderer.DrawRectangle(
            Position,
            Size * 0.66,
            new Vector4D<float>(0.32f, 0.16f, 0.06f, 1.0f),
            RotationRadians);
    }
}
