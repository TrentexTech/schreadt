using Schreadt_Engine.Component.Logic;
using Silk.NET.Maths;

namespace Schreadt_Engine.Component;

public abstract class GameObject : IUpdateable, IRenderable
{
    protected bool active = false;
    protected Vector2D<double> _position;
    protected List<GameObject> _children = [];

    public bool Active
    {
        get => active;
        set => active = value;
    }

    public abstract void Update(double dt);

    public abstract void Render();

    public void AddChild(GameObject child)
    {
        _children.Add(child);
    }
}

public interface IRenderable
{
    void Render();
}