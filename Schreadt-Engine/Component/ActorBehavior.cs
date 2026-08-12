using Schreadt_Engine.Component.Logic;

namespace Schreadt_Engine.Component;

/// <summary>
/// A lifecycle-capable component that can only be attached to an <see cref="Actor"/>.
/// </summary>
public abstract class ActorBehavior : GameComponent,
    IInitializable, IUpdateable, IFixedUpdateable, IShutdownable
{
    protected Actor Actor => (Actor)Owner;

    public abstract void Init();

    public abstract void Update(double dt);

    public virtual void FixedUpdate(double dt)
    {
    }

    public virtual void Shutdown()
    {
    }

    protected override void OnAttached()
    {
        if (Owner is not Schreadt_Engine.Component.Actor)
            throw new InvalidOperationException($"{GetType().Name} can only be attached to an Actor.");
    }
}
