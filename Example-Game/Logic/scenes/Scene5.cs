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

    protected override string HudNote => "PROVISIONAL: SLOPES, ROTATING BEAM, ORIENTED CRATE";

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

        Scene.AddChild(new ProvisionalOrientedCrate(0.35)
        {
            Position = new Vector2D<double>(9.55, 0.95)
        });

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
        _elapsed = (_elapsed + dt) % (Math.Tau / _angularFrequency);
        RotationRadians = _centerRotation + Math.Sin(_elapsed * _angularFrequency) * _rotationAmplitude;
    }
}

internal sealed class ProvisionalOrientedCrate : Rectangle2D
{
    internal RigidBody2D Body { get; }
    internal OrientedBoxCollider2D Collider { get; }

    internal ProvisionalOrientedCrate(double rotation)
    {
        Size = new Vector2D<double>(0.62, 0.46);
        RotationRadians = rotation;
        Color = new Vector4D<float>(0.86f, 0.52f, 0.16f, 1.0f);
        RenderLayer = 21;
        Body = AddComponent(new RigidBody2D
        {
            BodyType = CollisionBodyType2D.Dynamic,
            Mass = 0.8,
            Friction = 0.7,
            Restitution = 0.05,
            LinearDamping = 0.8,
            MaximumSpeed = 4.5
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
