using Schreadt_Engine.Collision;
using Schreadt_Engine.Component.Logic;
using Schreadt_Engine.Core;
using Schreadt_Engine.Gui;

namespace Schreadt_Engine.Component;

public class Scene : GameObject
{
    private bool _unloaded;

    public string Name { get; }
    public SceneLogic Logic { get; }
    public CollisionWorld2D Collisions { get; } = new();
    public GridBackground2D? Background { get; set; } = new();
    public GuiLayer Gui { get; } = new();
    public GuiScreenStack Screens => Gui.Screens;

    internal Scene(string name, SceneLogic logic, RuntimeController? runtime = null)
    {
        Name = name;
        Logic = logic;
        Logic.Attach(this);
        Screens.SetRuntime(runtime);
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

    protected override void OnFixedUpdate(double dt)
    {
        Logic.FixedUpdate(dt);
    }

    protected override void OnShutdown()
    {
        Logic.Shutdown();
    }

    internal void Unload()
    {
        if (_unloaded) return;

        Shutdown();
        DetachFromScene();
        Collisions.Clear();
        Gui.Clear();
        Screens.SetRuntime(null);
        _unloaded = true;
    }
}
