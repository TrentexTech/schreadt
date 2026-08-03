using Example_Game.Logic;
using Schreadt_Engine.Collision;
using Schreadt_Engine.Component.Logic;
using Schreadt_Engine.Component.PreFab;
using Schreadt_Engine.Core;
using Silk.NET.Maths;

namespace Example_Game.Logic.scenes;

public class Scene1 : SceneLogic
{
    private readonly ExamplePhysicsTuning _physicsTuning;

    public Scene1(ExamplePhysicsTuning physicsTuning)
    {
        _physicsTuning = physicsTuning;
    }

    public override void Update(double dt)
    {
    }

    public override void Init()
    {
        var player = new Circle(new PlayerCircleLogic())
        {
            Position = new Vector2D<double>(-0.6, 0.0),
            Color = new Vector4D<float>(0.45f, 0.9f, 0.45f, 1.0f)
        };
        var landmark = new Circle
        {
            Position = new Vector2D<double>(-1.35, -0.45),
            Radius = 0.22,
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
            Color = new Vector4D<float>(1.0f, 0.55f, 0.2f, 0.65f)
        };
        var energyBeacon = new Sprite("example/energy-beacon")
        {
            Position = new Vector2D<double>(0.65, -0.4),
            Size = new Vector2D<double>(0.6, 0.6),
            RotationRadians = 0.12,
            Tint = new Vector4D<float>(0.8f, 0.95f, 1.0f, 0.9f)
        };
        player.AddComponent(new RigidBody2D
        {
            BodyType = CollisionBodyType2D.Kinematic
        });
        player.AddComponent(new CircleCollider2D(player.Radius));
        landmark.AddComponent(new CircleCollider2D(landmark.Radius));
        var fallingBody = fallingCircle.AddComponent(new RigidBody2D
        {
            BodyType = CollisionBodyType2D.Dynamic,
            Mass = 1.5,
            Restitution = 0.2,
            Friction = 0.8,
            LinearDamping = 0.25,
            MaximumSpeed = 3.0
        });
        fallingCircle.AddComponent(new CircleCollider2D(fallingCircle.Radius));
        fallingBody.AddImpulse(new Vector2D<double>(0.0, _physicsTuning.InitialImpulse));

        Scene.AddChild(energyBeacon);
        Scene.AddChild(player);
        Scene.AddChild(landmark);
        Scene.AddChild(fallingCircle);
        Scene.AddChild(nonCollidingDecoration);
        Scene.Collisions.Gravity = new Vector2D<double>(0.0, _physicsTuning.Gravity);

        State.CurrentReality.MainCamera.InitLogic(new FollowTargetCameraLogic(player));
    }
}
