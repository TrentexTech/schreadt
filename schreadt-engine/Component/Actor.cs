using Schreadt_Engine.Component.Logic;

namespace Schreadt_Engine.Component;

public abstract class Actor : GameObject
{
    public ActorLogic? ActorLogic { get; }

    protected Actor(ActorLogic actorLogic)
    {
        ActorLogic = actorLogic;

        ActorLogic.Actor = this;
    }

    protected override void OnInit()
    {
        ActorLogic?.Init();
    }

    protected override void OnUpdate(double dt)
    {
        ActorLogic?.Update(dt);
    }
}