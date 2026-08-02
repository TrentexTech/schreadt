namespace Schreadt_Engine.Component.Logic;

public abstract class ActorLogic : ILogic
{
    public Actor Actor { get; set; }

    public abstract void Update(double dt);
    public abstract void Init();
}