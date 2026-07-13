using Schreadt_Engine.Component.Logic;

namespace Schreadt_Engine.Component;

public abstract class Actor : GameObject
{
    public ActorLogic ActorLogic;

    public override void Update(double dt)
    {
        ActorLogic.Update(dt);
    }

    public override void Render()
    {
    }
}