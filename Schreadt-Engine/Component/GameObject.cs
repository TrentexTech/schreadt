using Schreadt_Engine.Component.Logic;
using Schreadt_Engine.Core;
using Silk.NET.Maths;

namespace Schreadt_Engine.Component;

public abstract class GameObject : IInitializable, IUpdateable, IFixedUpdateable, IShutdownable, IRenderable
{
    private bool _initialized;
    private GameObject? _parent;
    private Scene? _scene;

    protected Vector2D<double> _position;
    private readonly List<GameObject> _children = [];
    private readonly List<GameComponent> _components = [];

    /// <summary>
    /// Whether this object is locally active. An inactive object also suppresses its descendants.
    /// </summary>
    public bool Active { get; set; } = true;

    /// <summary>
    /// Whether this object and every object above it in the hierarchy are active.
    /// </summary>
    public bool ActiveInHierarchy
    {
        get
        {
            for (GameObject? current = this; current is not null; current = current._parent)
            {
                if (!current.Active) return false;
            }

            return true;
        }
    }

    /// <summary>
    /// Broad rendering group. Lower layers are drawn first and therefore appear behind higher layers.
    /// </summary>
    public int RenderLayer { get; set; }

    /// <summary>
    /// Ordering within <see cref="RenderLayer"/>. Lower values are drawn first.
    /// Equal values preserve hierarchy traversal and insertion order.
    /// </summary>
    public int RenderOrder { get; set; }

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

        foreach (var component in _components.ToArray())
        {
            if (component.IsOwnedBy(this) && component is IInitializable initializable) initializable.Init();
        }

        foreach (var child in _children.ToArray())
        {
            child.Init();
        }

        _initialized = true;
    }

    public void Update(double dt)
    {
        EnsureInitialized();
        if (!ActiveInHierarchy) return;

        OnUpdate(dt);

        foreach (var component in _components.ToArray())
        {
            if (component.IsOwnedBy(this) && component is IUpdateable updateable) updateable.Update(dt);
        }

        foreach (var child in _children.ToArray())
        {
            if (ReferenceEquals(child.Parent, this)) child.Update(dt);
        }
    }

    public void FixedUpdate(double dt)
    {
        EnsureInitialized();
        if (!ActiveInHierarchy) return;

        OnFixedUpdate(dt);

        foreach (var component in _components.ToArray())
        {
            if (component.IsOwnedBy(this) && component is IFixedUpdateable fixedUpdateable)
                fixedUpdateable.FixedUpdate(dt);
        }

        foreach (var child in _children.ToArray())
        {
            if (ReferenceEquals(child.Parent, this)) child.FixedUpdate(dt);
        }
    }

    public void Shutdown()
    {
        if (!_initialized) return;

        OnShutdown();

        foreach (var child in _children.ToArray().Reverse())
        {
            if (ReferenceEquals(child.Parent, this)) child.Shutdown();
        }

        foreach (var component in _components.ToArray().Reverse())
        {
            if (component.IsOwnedBy(this) && component is IShutdownable shutdownable) shutdownable.Shutdown();
        }

        _initialized = false;
    }

    public void Render(IRenderContext2D renderer)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        EnsureInitialized();
        if (!ActiveInHierarchy) return;

        var renderEntries = new List<RenderEntry>();
        long sequence = 0;
        CollectRenderEntries(renderEntries, ref sequence);
        renderEntries.Sort(RenderEntryComparer.Instance);

        foreach (var entry in renderEntries)
        {
            if (entry.Object.CanRenderWithin(this)) entry.Object.OnRender(renderer);
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

        child.Shutdown();
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
        var registeredWithScene = false;

        try
        {
            if (_scene is not null)
            {
                _scene.RegisterComponent(component);
                registeredWithScene = true;
            }

            if (_initialized && component is IInitializable initializable) initializable.Init();
        }
        catch
        {
            if (registeredWithScene) _scene!.UnregisterComponent(component);
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
        if (_initialized && component is IShutdownable shutdownable) shutdownable.Shutdown();
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

    protected virtual void OnFixedUpdate(double dt)
    {
    }

    protected virtual void OnShutdown()
    {
    }

    protected virtual void OnRender(IRenderContext2D renderer)
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

    private void CollectRenderEntries(List<RenderEntry> entries, ref long sequence)
    {
        if (!ActiveInHierarchy) return;

        entries.Add(new RenderEntry(this, sequence++));
        foreach (var child in _children.ToArray())
        {
            if (ReferenceEquals(child.Parent, this)) child.CollectRenderEntries(entries, ref sequence);
        }
    }

    private bool CanRenderWithin(GameObject renderRoot)
    {
        if (!ActiveInHierarchy) return false;

        for (GameObject? current = this; current is not null; current = current.Parent)
        {
            if (!current._initialized) return false;
            if (ReferenceEquals(current, renderRoot)) return true;
        }

        return false;
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

    private readonly record struct RenderEntry(GameObject Object, long Sequence);

    private sealed class RenderEntryComparer : IComparer<RenderEntry>
    {
        internal static RenderEntryComparer Instance { get; } = new();

        public int Compare(RenderEntry first, RenderEntry second)
        {
            var layerComparison = first.Object.RenderLayer.CompareTo(second.Object.RenderLayer);
            if (layerComparison != 0) return layerComparison;

            var orderComparison = first.Object.RenderOrder.CompareTo(second.Object.RenderOrder);
            return orderComparison != 0 ? orderComparison : first.Sequence.CompareTo(second.Sequence);
        }
    }
}

public interface IRenderable
{
    void Render(IRenderContext2D renderer);
}
