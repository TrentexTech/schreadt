using Example_Game.Logic;
using Schreadt_Engine.Collision;
using Schreadt_Engine.Component.Logic;
using Schreadt_Engine.Component.PreFab;
using Schreadt_Engine.Core;
using Silk.NET.Maths;

namespace Example_Game.Logic.scenes;

public class Scene0 : SceneLogic
{
    private readonly ExamplePhysicsTuning _physicsTuning;
    private readonly IInputState _input;

    public Scene0(ExamplePhysicsTuning physicsTuning, IInputState input)
    {
        _physicsTuning = physicsTuning;
        _input = input;
    }

    public override void Update(double dt)
    {
    }

    public override void Init()
    {
        var player = new Circle(new PlayerCircleLogic(_input));
        player.RenderLayer = 10;
        var landmark = new Circle
        {
            Position = new Vector2D<double>(1.2, 0.45),
            Radius = 0.18,
            Color = new Vector4D<float>(1.0f, 0.35f, 0.12f, 1.0f)
        };
        var fallingCircle = new Circle
        {
            Position = new Vector2D<double>(1.2, 1.15),
            Radius = 0.14,
            Color = new Vector4D<float>(1.0f, 0.9f, 0.2f, 1.0f)
        };
        var nonCollidingDecoration = new Circle
        {
            Position = new Vector2D<double>(-1.1, 0.55),
            Radius = 0.25,
            RenderLayer = -20,
            Color = new Vector4D<float>(0.2f, 0.85f, 0.75f, 0.65f)
        };
        var energyBeacon = new Sprite("example/energy-beacon")
        {
            Position = new Vector2D<double>(-0.6, -0.4),
            Size = new Vector2D<double>(0.55, 0.55),
            RenderLayer = -30,
            RotationRadians = -0.08
        };
        var tiltedPanel = new Rectangle2D
        {
            Position = new Vector2D<double>(-1.35, -0.65),
            Size = new Vector2D<double>(0.62, 0.24),
            RenderLayer = -20,
            RotationRadians = 0.18,
            Color = new Vector4D<float>(0.95f, 0.3f, 0.45f, 0.85f)
        };
        var markerTriangle = new Triangle
        {
            Position = new Vector2D<double>(-1.35, -0.28),
            Scale = new Vector2D<double>(0.3, 0.3),
            RenderLayer = -20,
            RotationRadians = -0.12,
            Color = new Vector4D<float>(0.95f, 0.85f, 0.25f, 0.9f)
        };
        var checkpoint = new TriggerZone2D(0.3)
        {
            Position = new Vector2D<double>(0.75, 0.0),
            RenderLayer = -10,
            Color = new Vector4D<float>(0.2f, 0.65f, 1.0f, 0.25f),
            Filter = candidate => ReferenceEquals(candidate, player)
        };
        checkpoint.Entered += _ => checkpoint.Color = new Vector4D<float>(0.25f, 1.0f, 0.45f, 0.55f);
        checkpoint.Exited += _ => checkpoint.Color = new Vector4D<float>(0.2f, 0.65f, 1.0f, 0.25f);
        player.AddComponent(new RigidBody2D
        {
            BodyType = CollisionBodyType2D.Kinematic
        });
        player.AddComponent(new CircleCollider2D(player.Radius));
        landmark.AddComponent(new CircleCollider2D(landmark.Radius));
        var fallingBody = fallingCircle.AddComponent(new RigidBody2D
        {
            BodyType = CollisionBodyType2D.Dynamic,
            Mass = 0.75,
            Restitution = 0.35,
            Friction = 0.7,
            LinearDamping = 0.15,
            MaximumSpeed = 3.0
        });
        fallingCircle.AddComponent(new CircleCollider2D(fallingCircle.Radius));
        fallingBody.AddImpulse(new Vector2D<double>(0.0, _physicsTuning.InitialImpulse));

        Scene.AddChild(energyBeacon);
        Scene.AddChild(tiltedPanel);
        Scene.AddChild(markerTriangle);
        Scene.AddChild(checkpoint);
        Scene.AddChild(player);
        Scene.AddChild(landmark);
        Scene.AddChild(fallingCircle);
        Scene.AddChild(nonCollidingDecoration);
        Scene.Collisions.Gravity = new Vector2D<double>(0.0, _physicsTuning.Gravity);

        State.CurrentReality.MainCamera.SetController(new FollowTargetCameraLogic(player));
    }
}
