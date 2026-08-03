namespace Schreadt_Engine.Component.Logic;

public interface IInitializable
{
    void Init();
}

public interface IUpdateable
{
    void Update(double dt);
}

public interface IFixedUpdateable
{
    void FixedUpdate(double dt);
}

public interface IShutdownable
{
    void Shutdown();
}

[Obsolete("Implement only the lifecycle capability interfaces the type needs.")]
public interface ILogic : IInitializable, IUpdateable
{
}
