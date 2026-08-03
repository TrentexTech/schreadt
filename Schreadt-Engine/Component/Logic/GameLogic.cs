using Schreadt_Engine.Core;

namespace Schreadt_Engine.Component.Logic;

public abstract class GameLogic : ILogic
{
    private Reality? _reality;

    protected Reality Reality => _reality
        ?? throw new InvalidOperationException("Game logic has not been attached to a reality.");

    internal void Attach(Reality reality)
    {
        ArgumentNullException.ThrowIfNull(reality);

        if (_reality is not null)
        {
            throw new InvalidOperationException("A game logic instance can only be attached once.");
        }

        _reality = reality;
    }

    public abstract void Update(double dt);

    public abstract void Init();
}
