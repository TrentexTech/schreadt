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

    public Scene0(ExamplePhysicsTuning physicsTuning)
    {
        _physicsTuning = physicsTuning;
    }

    public override void Update(double dt)
    {
    }

    public override void Init()
    {
        var player = new Circle(new PlayerCircleLogic());
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
            Color = new Vector4D<float>(0.2f, 0.85f, 0.75f, 0.65f)
        };
        var energyBeacon = new Sprite("example/energy-beacon")
        {
            Position = new Vector2D<double>(-0.6, -0.4),
            Size = new Vector2D<double>(0.55, 0.55),
            RotationRadians = -0.08
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
            Mass = 0.75,
            Restitution = 0.35,
            Friction = 0.7,
            LinearDamping = 0.15,
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
