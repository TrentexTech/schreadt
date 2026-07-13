namespace Schreadt_Engine.Component.Logic;

public abstract class ActorLogic : ILogic
{
    public abstract void Update(double dt);
    public abstract void Init();
}