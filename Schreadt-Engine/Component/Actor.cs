using Schreadt_Engine.Component.Logic;

namespace Schreadt_Engine.Component;

public abstract class Actor : GameObject
{
    public ActorLogic? ActorLogic { get; }

    protected Actor(ActorLogic? actorLogic = null)
    {
        ActorLogic = actorLogic;

        if (ActorLogic is not null) ActorLogic.Actor = this;
    }

    protected override void OnInit()
    {
        ActorLogic?.Init();
    }

    protected override void OnUpdate(double dt)
    {
        ActorLogic?.Update(dt);
    }

    protected override void OnFixedUpdate(double dt)
    {
        ActorLogic?.FixedUpdate(dt);
    }

    protected override void OnShutdown()
    {
        ActorLogic?.Shutdown();
    }
}
