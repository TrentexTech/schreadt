namespace Schreadt_Engine.Component.Logic;

public interface ILogic : IUpdateable
{
    public void Init();
}

public interface IUpdateable
{
    public void Update(double dt);
}