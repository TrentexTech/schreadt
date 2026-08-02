using Example_Game.Logic;
using Schreadt_Engine.Collision;
using Schreadt_Engine.Component.Logic;
using Schreadt_Engine.Component.PreFab;
using Schreadt_Engine.Core;
using Silk.NET.Maths;

namespace Example_Game.Logic.scenes;

public class Scene0 : SceneLogic
{
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
        var playerBody = new RigidBody2D(player)
        {
            BodyType = CollisionBodyType2D.Dynamic,
            UseGravity = false
        };
        var fallingBody = new RigidBody2D(fallingCircle)
        {
            BodyType = CollisionBodyType2D.Dynamic
        };

        Scene.AddChild(player);
        Scene.AddChild(landmark);
        Scene.AddChild(fallingCircle);
        Scene.Collisions.Gravity = new Vector2D<double>(0.0, -2.5);
        Scene.Collisions.AddCollider(new CircleCollider2D(playerBody, player.Radius));
        Scene.Collisions.AddCollider(new CircleCollider2D(landmark, landmark.Radius));
        Scene.Collisions.AddCollider(new CircleCollider2D(fallingBody, fallingCircle.Radius));

        State.CurrentReality.MainCamera.InitLogic(new FollowTargetCameraLogic(player));
    }
}
