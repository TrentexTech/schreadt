using Schreadt_Engine.Collision;
using Schreadt_Engine.Component.Logic;
using Schreadt_Engine.Core;
using Schreadt_Engine.Gui;

namespace Schreadt_Engine.Component;

public class Scene : GameObject
{
    private readonly List<IFrameCompositionPass2D> _compositionPasses = [];
    private bool _unloaded;

    internal IEngineContext? EngineContext { get; }

    public string Name { get; }
    public SceneLogic Logic { get; }
    public CollisionWorld2D Collisions { get; } = new();
    public IBackground2D? Background { get; set; } = new GridBackground2D();
    public IReadOnlyList<IFrameCompositionPass2D> CompositionPasses => _compositionPasses;
    public GuiLayer Gui { get; } = new();
    public GuiScreenStack Screens => Gui.Screens;

    internal Scene(
        string name,
        SceneLogic logic,
        RuntimeController? runtime = null,
        IEngineContext? context = null)
    {
        Name = name;
        Logic = logic;
        EngineContext = context;
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

    public T AddCompositionPass<T>(T pass) where T : IFrameCompositionPass2D
    {
        ArgumentNullException.ThrowIfNull(pass);
        ValidateCompositionPass(pass);
        if (_compositionPasses.Any(existing => ReferenceEquals(existing, pass)))
            throw new InvalidOperationException("The composition pass is already registered with this scene.");
        if (_compositionPasses.Any(existing => string.Equals(existing.Name, pass.Name, StringComparison.Ordinal)))
            throw new InvalidOperationException($"A composition pass named '{pass.Name}' is already registered.");

        _compositionPasses.Add(pass);
        return pass;
    }

    public bool RemoveCompositionPass(IFrameCompositionPass2D pass)
    {
        ArgumentNullException.ThrowIfNull(pass);
        return _compositionPasses.Remove(pass);
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
        _compositionPasses.Clear();
        Gui.Clear();
        Screens.SetRuntime(null);
        _unloaded = true;
    }

    private static void ValidateCompositionPass(IFrameCompositionPass2D pass)
    {
        if (string.IsNullOrWhiteSpace(pass.Name))
            throw new ArgumentException("A composition pass must have a non-empty name.", nameof(pass));
        if (!Enum.IsDefined(pass.Stage))
            throw new ArgumentOutOfRangeException(nameof(pass), "The composition pass stage is invalid.");
    }
}
