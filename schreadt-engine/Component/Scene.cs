using Schreadt_Engine.Collision;
using Schreadt_Engine.Component.Logic;

namespace Schreadt_Engine.Component;

public class Scene : GameObject
{
    private bool _unloaded;

    public string Name { get; }
    public SceneLogic Logic { get; }
    public CollisionWorld2D Collisions { get; } = new();
    public GridBackground2D? Background { get; set; } = new();

    internal Scene(string name, SceneLogic logic)
    {
        Name = name;
        Logic = logic;
        Logic.Attach(this);
        AttachToScene(this);
    }

    internal void RegisterComponent(GameComponent component)
    {
        if (component is Collider2D collider) Collisions.AddCollider(collider);
    }

    internal void UnregisterComponent(GameComponent component)
    {
        if (component is Collider2D collider) Collisions.RemoveCollider(collider);
    }

    protected override void OnInit()
    {
        Logic.Init();
    }

    protected override void OnUpdate(double dt)
    {
        Logic.Update(dt);
    }

    internal void Unload()
    {
        if (_unloaded) return;

        Logic.Unload();
        DetachFromScene();
        Collisions.Clear();
        _unloaded = true;
    }
}
