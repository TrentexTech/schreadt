using Schreadt_Engine.Component.Logic;

namespace Schreadt_Engine.Component.PreFab;

public class Circle : Actor
{
    public Circle()
    {
        ActorLogic = new CircleLogic();
    }
}

public class CircleLogic : ActorLogic
{
    public override void Update(double dt)
    {
        throw new NotImplementedException();
    }

    public override void Init()
    {
        throw new NotImplementedException();
    }
}