namespace Schreadt_Engine.Component.Logic;

public abstract class SceneLogic : ILogic
{
    public Scene Scene;

    public abstract void Update(double dt);

    public abstract void Init();
}