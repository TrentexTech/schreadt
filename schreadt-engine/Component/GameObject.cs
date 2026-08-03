using Schreadt_Engine.Component.Logic;
using Schreadt_Engine.Core;
using Silk.NET.Maths;

namespace Schreadt_Engine.Component;

public abstract class GameObject : IUpdateable, IRenderable
{
    private bool _initialized;
    private GameObject? _parent;
    private Scene? _scene;

    protected Vector2D<double> _position;
    private readonly List<GameObject> _children = [];
    private readonly List<GameComponent> _components = [];

    public bool Active { get; set; } = true;

    public bool Initialized => _initialized;

    public GameObject? Parent => _parent;

    public Scene? Scene => _scene;

    public IReadOnlyList<GameObject> Children => _children;

    public IReadOnlyList<GameComponent> Components => _components;

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
        if (child.Scene is not null) throw new InvalidOperationException("The game object already belongs to a scene.");

        _children.Add(child);
        child._parent = this;

        try
        {
            if (_scene is not null) child.AttachToScene(_scene);
            if (_initialized) child.Init();
        }
        catch
        {
            if (child._scene is not null) child.DetachFromScene();
            _children.Remove(child);
            child._parent = null;
            throw;
        }
    }

    public bool RemoveChild(GameObject child)
    {
        ArgumentNullException.ThrowIfNull(child);

        if (!_children.Contains(child)) return false;

        if (child._scene is not null) child.DetachFromScene();
        _children.Remove(child);
        child._parent = null;
        return true;
    }

    public T AddComponent<T>(T component) where T : GameComponent
    {
        ArgumentNullException.ThrowIfNull(component);

        component.Attach(this);
        _components.Add(component);

        try
        {
            _scene?.RegisterComponent(component);
        }
        catch
        {
            _components.Remove(component);
            component.Detach();
            throw;
        }

        return component;
    }

    public bool RemoveComponent(GameComponent component)
    {
        ArgumentNullException.ThrowIfNull(component);

        if (!component.IsOwnedBy(this) || !_components.Contains(component)) return false;

        component.ValidateCanDetach();
        _scene?.UnregisterComponent(component);
        _components.Remove(component);
        component.Detach();
        return true;
    }

    public T? GetComponent<T>() where T : GameComponent
    {
        return _components.OfType<T>().FirstOrDefault();
    }

    public IReadOnlyList<T> GetComponents<T>() where T : GameComponent
    {
        return _components.OfType<T>().ToArray();
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

    internal void AttachToScene(Scene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);

        if (_scene is not null)
            throw new InvalidOperationException("The game object already belongs to a scene.");

        _scene = scene;
        var registeredComponents = new List<GameComponent>();
        var attachedChildren = new List<GameObject>();

        try
        {
            foreach (var component in _components)
            {
                scene.RegisterComponent(component);
                registeredComponents.Add(component);
            }

            foreach (var child in _children)
            {
                child.AttachToScene(scene);
                attachedChildren.Add(child);
            }
        }
        catch
        {
            foreach (var child in attachedChildren.AsEnumerable().Reverse()) child.DetachFromScene();
            foreach (var component in registeredComponents.AsEnumerable().Reverse())
                scene.UnregisterComponent(component);

            _scene = null;
            throw;
        }
    }

    internal void DetachFromScene()
    {
        var scene = _scene;
        if (scene is null) return;

        foreach (var child in _children.ToArray()) child.DetachFromScene();
        foreach (var component in _components.ToArray()) scene.UnregisterComponent(component);
        _scene = null;
    }
}

public interface IRenderable
{
    void Render(Renderer renderer);
}
