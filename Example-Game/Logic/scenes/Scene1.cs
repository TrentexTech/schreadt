using Example_Game.Logic;
using Schreadt_Engine.Collision;
using Schreadt_Engine.Component.Logic;
using Schreadt_Engine.Component.PreFab;
using Schreadt_Engine.Core;
using Silk.NET.Maths;

namespace Example_Game.Logic.scenes;

public class Scene1 : SceneLogic
{
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

        Scene.AddChild(player);
        Scene.AddChild(landmark);
        Scene.Collisions.AddCollider(new CircleCollider2D(player, player.Radius)
        {
            BodyType = CollisionBodyType2D.Dynamic
        });
        Scene.Collisions.AddCollider(new CircleCollider2D(landmark, landmark.Radius));

        State.CurrentReality.MainCamera.InitLogic(new FollowTargetCameraLogic(player));
    }
}
