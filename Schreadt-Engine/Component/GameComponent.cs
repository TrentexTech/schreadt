using Schreadt_Engine.Core;

namespace Schreadt_Engine.Component;

public abstract class GameComponent
{
    private GameObject? _owner;

    public GameObject Owner => _owner
        ?? throw new InvalidOperationException("The component is not attached to a game object.");

    public bool Attached => _owner is not null;

    protected IEngineContext Context => Owner.Context;

    internal bool IsOwnedBy(GameObject owner) => ReferenceEquals(_owner, owner);

    internal void Attach(GameObject owner)
    {
        ArgumentNullException.ThrowIfNull(owner);

        if (_owner is not null)
            throw new InvalidOperationException("A component can only belong to one game object.");

        _owner = owner;
        try
        {
            OnAttached();
        }
        catch
        {
            _owner = null;
            throw;
        }
    }

    internal virtual void ValidateCanDetach()
    {
    }

    internal void Detach()
    {
        try
        {
            OnDetached();
        }
        finally
        {
            _owner = null;
        }
    }

    protected virtual void OnAttached()
    {
    }

    protected virtual void OnDetached()
    {
    }
}
