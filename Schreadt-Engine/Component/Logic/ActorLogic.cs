namespace Schreadt_Engine.Component.Logic;

public abstract class ActorLogic : IInitializable, IUpdateable, IFixedUpdateable, IShutdownable
{
    public Actor Actor { get; internal set; } = null!;

    public abstract void Update(double dt);
    public abstract void Init();

    public virtual void FixedUpdate(double dt)
    {
    }

    public virtual void Shutdown()
    {
    }
}
