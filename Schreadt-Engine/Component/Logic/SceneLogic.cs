namespace Schreadt_Engine.Component.Logic;

public abstract class SceneLogic : IInitializable, IUpdateable, IFixedUpdateable, IShutdownable
{
    private Scene? _scene;

    protected Scene Scene => _scene
        ?? throw new InvalidOperationException("Scene logic has not been attached to a scene.");

    internal void Attach(Scene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);

        if (_scene is not null)
        {
            throw new InvalidOperationException("A scene logic instance can only be attached once.");
        }

        _scene = scene;
    }

    public abstract void Update(double dt);

    public abstract void Init();

    public virtual void FixedUpdate(double dt)
    {
    }

    public virtual void Shutdown()
    {
    }
}
