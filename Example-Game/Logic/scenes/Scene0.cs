using Example_Game.Logic;
using Schreadt_Engine.Component;
using Schreadt_Engine.Component.Logic;
using Schreadt_Engine.Component.PreFab;
using Schreadt_Engine.Core;
using Silk.NET.Maths;

namespace Example_Game.Logic.scenes;

public class Scene0 : SceneLogic
{
    public Scene0(Scene scene)
    {
        Scene = scene;
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

        Scene.AddChild(player);
        Scene.AddChild(landmark);

        State.CurrentReality.MainCamera.InitLogic(new FollowTargetCameraLogic(player));
    }
}
