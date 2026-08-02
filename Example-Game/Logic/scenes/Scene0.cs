using Schreadt_Engine.Component;
using Schreadt_Engine.Component.Logic;
using Schreadt_Engine.Component.PreFab;
using Example_Game.Logic;

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
        Scene.AddChild(new Circle(new PlayerCircleLogic()));
    }
}
