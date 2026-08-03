using Example_Game.Logic;
using Schreadt_Engine.Collision;
using Schreadt_Engine.Component;
using Schreadt_Engine.Component.Logic;
using Schreadt_Engine.Component.PreFab;
using Schreadt_Engine.Core;
using Silk.NET.Maths;

namespace Example_Game.Logic.scenes;

public class Scene1 : SceneLogic
{
    private readonly ExamplePhysicsTuning _physicsTuning;
    private readonly IInputState _input;

    public Scene1(ExamplePhysicsTuning physicsTuning, IInputState input)
    {
        _physicsTuning = physicsTuning;
        _input = input;
    }

    public override void Update(double dt)
    {
    }

    public override void Init()
    {
        ExampleGameScreens.AddSceneHud(Scene, "ALTERNATE");
        var player = new Circle(new PlayerCircleLogic(_input))
        {
            Position = new Vector2D<double>(-0.6, 0.0),
            RenderLayer = 10,
            Color = new Vector4D<float>(0.45f, 0.9f, 0.45f, 1.0f)
        };
        var landmark = new Rectangle2D
        {
            Position = new Vector2D<double>(-1.35, -0.45),
            Size = new Vector2D<double>(0.62, 0.2),
            Color = new Vector4D<float>(0.75f, 0.3f, 1.0f, 1.0f)
        };
        var fallingCircle = new Circle
        {
            Position = new Vector2D<double>(-1.35, 0.35),
            Radius = 0.14,
            Color = new Vector4D<float>(0.25f, 0.85f, 1.0f, 1.0f)
        };
        var nonCollidingDecoration = new Circle
        {
            Position = new Vector2D<double>(0.75, 0.65),
            Radius = 0.24,
            RenderLayer = -20,
            Color = new Vector4D<float>(1.0f, 0.55f, 0.2f, 0.65f)
        };
        var energyBeacon = new Sprite("example/energy-beacon")
        {
            Position = new Vector2D<double>(0.65, -0.4),
            Size = new Vector2D<double>(0.6, 0.6),
            RenderLayer = -30,
            RotationRadians = 0.12,
            Tint = new Vector4D<float>(0.8f, 0.95f, 1.0f, 0.9f)
        };
        ExampleSpriteAnimations.AddBeaconPulse(energyBeacon);
        var tiltedPanel = new Rectangle2D
        {
            Position = new Vector2D<double>(1.35, -0.65),
            Size = new Vector2D<double>(0.58, 0.22),
            RenderLayer = -20,
            RotationRadians = -0.2,
            Color = new Vector4D<float>(0.25f, 0.8f, 0.95f, 0.85f)
        };
        ExampleTweens.AddPanelSway(tiltedPanel, -0.18);
        var hexagon = new Polygon(
        [
            new Vector2D<double>(0.5, 0.0),
            new Vector2D<double>(0.25, 0.433),
            new Vector2D<double>(-0.25, 0.433),
            new Vector2D<double>(-0.5, 0.0),
            new Vector2D<double>(-0.25, -0.433),
            new Vector2D<double>(0.25, -0.433)
        ])
        {
            Position = new Vector2D<double>(1.35, -0.28),
            Scale = new Vector2D<double>(0.32, 0.32),
            RenderLayer = -20,
            RotationRadians = 0.16,
            Color = new Vector4D<float>(0.65f, 0.35f, 1.0f, 0.9f)
        };
        var checkpoint = new TriggerZone2D(0.32)
        {
            Position = new Vector2D<double>(0.15, 0.15),
            RenderLayer = -10,
            CollisionLayer = ExampleCollisionLayers.Trigger,
            CollisionMask = ExampleCollisionLayers.TriggerMask,
            Color = new Vector4D<float>(0.75f, 0.3f, 1.0f, 0.25f),
            Filter = candidate => ReferenceEquals(candidate, player)
        };
        checkpoint.Entered += _ => checkpoint.Color = new Vector4D<float>(1.0f, 0.45f, 0.75f, 0.55f);
        checkpoint.Exited += _ => checkpoint.Color = new Vector4D<float>(0.75f, 0.3f, 1.0f, 0.25f);
        player.AddComponent(new RigidBody2D
        {
            BodyType = CollisionBodyType2D.Kinematic
        });
        player.AddComponent(new CircleCollider2D(player.Radius)
        {
            CollisionLayer = ExampleCollisionLayers.Player,
            CollisionMask = ExampleCollisionLayers.PlayerMask
        });
        landmark.AddComponent(new AxisAlignedBoxCollider2D(landmark.Size)
        {
            CollisionLayer = ExampleCollisionLayers.World,
            CollisionMask = ExampleCollisionLayers.WorldMask
        });
        var fallingBody = fallingCircle.AddComponent(new RigidBody2D
        {
            BodyType = CollisionBodyType2D.Dynamic,
            Mass = 1.5,
            Restitution = 0.2,
            Friction = 0.8,
            LinearDamping = 0.25,
            MaximumSpeed = 3.0
        });
        var fallingCollider = fallingCircle.AddComponent(new CircleCollider2D(fallingCircle.Radius)
        {
            CollisionLayer = ExampleCollisionLayers.Hazard,
            CollisionMask = ExampleCollisionLayers.HazardMask
        });
        fallingBody.AddImpulse(new Vector2D<double>(0.0, _physicsTuning.InitialImpulse));

        Scene.AddChild(energyBeacon);
        Scene.AddChild(tiltedPanel);
        Scene.AddChild(hexagon);
        Scene.AddChild(checkpoint);
        Scene.AddChild(player);
        Scene.AddChild(landmark);
        Scene.AddChild(fallingCircle);
        Scene.AddChild(nonCollidingDecoration);
        Scene.Collisions.Gravity = new Vector2D<double>(0.0, _physicsTuning.Gravity);

        var camera = State.CurrentReality.MainCamera;
        camera.SetController(new FollowCameraController2D(player)
        {
            SmoothTime = 0.12,
            DeadZone = new Vector2D<double>(0.18, 0.12)
        });
        var cameraShake = camera.GetComponent<CameraShake2D>() ?? camera.AddComponent(new CameraShake2D());
        fallingCollider.CollisionEntered += contact =>
        {
            if (ReferenceEquals(contact.Other.Owner, player)) cameraShake.Shake(0.24, 0.045, 0.018);
        };
    }
}
