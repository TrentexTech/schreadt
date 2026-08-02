using Schreadt_Engine.Component.Logic;
using Schreadt_Engine.Core;
using Silk.NET.Maths;

namespace Schreadt_Engine.Component;

public abstract class GameObject : IUpdateable, IRenderable
{
    private bool _initialized;
    private GameObject? _parent;

    protected Vector2D<double> _position;
    private readonly List<GameObject> _children = [];

    public bool Active { get; set; } = true;

    public bool Initialized => _initialized;

    public GameObject? Parent => _parent;

    public IReadOnlyList<GameObject> Children => _children;

    public Vector2D<double> Position
    {
        get => _position;
        set => _position = value;
    }

    public void Init()
    {
        if (_initialized) return;

        OnInit();

        foreach (var child in _children.ToArray())
        {
            child.Init();
        }

        _initialized = true;
    }

    public void Update(double dt)
    {
        EnsureInitialized();
        if (!Active) return;

        OnUpdate(dt);

        foreach (var child in _children.ToArray())
        {
            if (ReferenceEquals(child.Parent, this)) child.Update(dt);
        }
    }

    public void Render(Renderer renderer)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        EnsureInitialized();
        if (!Active) return;

        OnRender(renderer);

        foreach (var child in _children.ToArray())
        {
            if (ReferenceEquals(child.Parent, this)) child.Render(renderer);
        }
    }

    public void AddChild(GameObject child)
    {
        ArgumentNullException.ThrowIfNull(child);

        if (ReferenceEquals(child, this)) throw new InvalidOperationException("A game object cannot be its own child.");

        for (var ancestor = this; ancestor is not null; ancestor = ancestor.Parent)
        {
            if (ReferenceEquals(ancestor, child)) throw new InvalidOperationException("A game object cannot adopt one of its ancestors.");
        }

        if (child.Parent is not null) throw new InvalidOperationException("The game object already has a parent.");

        _children.Add(child);
        child._parent = this;

        if (_initialized) child.Init();
    }

    public bool RemoveChild(GameObject child)
    {
        ArgumentNullException.ThrowIfNull(child);

        if (!_children.Remove(child)) return false;

        child._parent = null;
        return true;
    }

    protected virtual void OnInit()
    {
    }

    protected virtual void OnUpdate(double dt)
    {
    }

    protected virtual void OnRender(Renderer renderer)
    {
    }
    
    public void Move(Vector2D<double> delta)
    {
        _position += delta;
    }

    public void Move(double x, double y)
    {
        _position += new Vector2D<double>(x, y);
    }

    private void EnsureInitialized()
    {
        if (!_initialized) throw new InvalidOperationException($"{GetType().Name} must be initialized before it can be updated or rendered.");
    }
}

public interface IRenderable
{
    void Render(Renderer renderer);
}
