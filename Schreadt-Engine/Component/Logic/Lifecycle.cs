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
