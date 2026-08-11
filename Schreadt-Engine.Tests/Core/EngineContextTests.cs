#pragma warning disable CS0618 // State is intentionally exercised as a compatibility facade.

using Schreadt_Engine.Asset;
using Schreadt_Engine.Component;
using Schreadt_Engine.Component.Logic;
using Schreadt_Engine.Core;
using Schreadt_Engine.Gui;
using Silk.NET.Maths;

namespace Schreadt_Engine.Tests.Core;

[Collection("Engine state")]
public sealed class EngineContextTests
{
    [Fact]
    public void FailedInitialization_ClearsAllGlobalStateAndAllowsSecondInitialization()
    {
        EngineMain.Shutdown();
        var failingLogic = new FailingGameLogic();

        try
        {
            var exception = Assert.Throws<InvalidOperationException>(() =>
                EngineMain.Init(failingLogic, ["failed-run"]));

            Assert.Equal("Injected initialization failure.", exception.Message);
            Assert.NotNull(failingLogic.CapturedContext);
            Assert.Null(State.CurrentContext);
            Assert.Empty(State.LaunchArgs);
            Assert.Throws<InvalidOperationException>(() => State.CurrentReality);
            Assert.Throws<InvalidOperationException>(() => State.Input);
            Assert.Throws<InvalidOperationException>(() => State.Gui);
            Assert.Throws<InvalidOperationException>(() => State.Assets);
            Assert.Throws<InvalidOperationException>(() => State.Window);
            Assert.Throws<InvalidOperationException>(() => State.Runtime);

            var successfulLogic = new SuccessfulGameLogic();
            EngineMain.Init(successfulLogic, ["second-run"]);

            Assert.Same(successfulLogic.CapturedContext, State.CurrentContext);
            Assert.Equal(["second-run"], successfulLogic.CapturedContext!.LaunchArgs);
            Assert.Equal("ready", successfulLogic.CapturedContext.Scenes.CurrentSceneName);
        }
        finally
        {
            EngineMain.Shutdown();
        }

        Assert.Null(State.CurrentContext);
        Assert.Empty(State.LaunchArgs);
    }

    [Fact]
    public void IndependentContexts_DoNotShareEngineServicesOrUseGlobalState()
    {
        EngineMain.Shutdown();
        using var first = ContextHarness.Create("first");
        using var second = ContextHarness.Create("second");

        Assert.NotSame(first.Context.Input, second.Context.Input);
        Assert.NotSame(first.Context.Assets, second.Context.Assets);
        Assert.NotSame(first.Context.Window, second.Context.Window);
        Assert.NotSame(first.Context.Scenes, second.Context.Scenes);
        Assert.NotSame(first.Context.Runtime, second.Context.Runtime);
        Assert.NotSame(first.Context.Gui, second.Context.Gui);
        Assert.NotSame(first.Context.MainCamera, second.Context.MainCamera);
        Assert.Same(first.Context, first.Logic.CapturedContext);
        Assert.Same(second.Context, second.Logic.CapturedContext);
        Assert.Same(first.Context, first.Logic.SceneLogic.CapturedContext);
        Assert.Same(second.Context, second.Logic.SceneLogic.CapturedContext);
        Assert.Same(first.Context, first.Logic.SceneLogic.Component.CapturedContext);
        Assert.Same(second.Context, second.Logic.SceneLogic.Component.CapturedContext);
        Assert.Same(first.Context, first.Reality.Scene.Context);
        Assert.Same(second.Context, second.Reality.Scene.Context);
        Assert.Equal("first", first.Context.Scenes.CurrentSceneName);
        Assert.Equal("second", second.Context.Scenes.CurrentSceneName);
        Assert.Null(State.CurrentContext);
    }

    private sealed class FailingGameLogic : GameLogic
    {
        internal IEngineContext? CapturedContext { get; private set; }

        public override void Init()
        {
            CapturedContext = Context;
            throw new InvalidOperationException("Injected initialization failure.");
        }

        public override void Update(double dt)
        {
        }
    }

    private sealed class SuccessfulGameLogic : GameLogic
    {
        internal IEngineContext? CapturedContext { get; private set; }

        public override void Init()
        {
            CapturedContext = Context;
            Context.Scenes.RegisterScene("ready", () => new EmptySceneLogic());
            Context.Scenes.LoadScene("ready");
        }

        public override void Update(double dt)
        {
        }
    }

    private sealed class RecordingGameLogic(string sceneName) : GameLogic
    {
        internal IEngineContext? CapturedContext { get; private set; }
        internal RecordingSceneLogic SceneLogic { get; } = new();

        public override void Init()
        {
            CapturedContext = Context;
            Context.Scenes.RegisterScene(sceneName, () => SceneLogic);
            Context.Scenes.LoadScene(sceneName);
        }

        public override void Update(double dt)
        {
        }
    }

    private sealed class RecordingSceneLogic : SceneLogic
    {
        internal IEngineContext? CapturedContext { get; private set; }
        internal RecordingComponent Component { get; } = new();

        public override void Init()
        {
            CapturedContext = Context;
            var gameObject = new TestGameObject();
            gameObject.AddComponent(Component);
            Scene.AddChild(gameObject);
        }

        public override void Update(double dt)
        {
        }
    }

    private sealed class RecordingComponent : GameComponent, IInitializable
    {
        internal IEngineContext? CapturedContext { get; private set; }

        public void Init() => CapturedContext = Context;
    }

    private sealed class TestGameObject : GameObject;

    private sealed class EmptySceneLogic : SceneLogic
    {
        public override void Init()
        {
        }

        public override void Update(double dt)
        {
        }
    }

    private sealed class ContextHarness : IDisposable
    {
        private readonly InputManager _input;
        private readonly AssetCatalog _assets;

        private ContextHarness(
            EngineContext context,
            Reality reality,
            RecordingGameLogic logic,
            InputManager input,
            AssetCatalog assets)
        {
            Context = context;
            Reality = reality;
            Logic = logic;
            _input = input;
            _assets = assets;
        }

        internal EngineContext Context { get; }
        internal Reality Reality { get; }
        internal RecordingGameLogic Logic { get; }

        internal static ContextHarness Create(string name)
        {
            var input = new InputManager();
            var assets = AssetCatalog.LoadFromSources([]);
            var gui = new GuiSystem();
            var runtime = new RuntimeController();
            var logic = new RecordingGameLogic(name);
            var reality = new Reality(logic, gui, runtime);
            var context = new EngineContext(
                [name], input, assets, new TestWindow(), reality, runtime, gui);
            reality.AttachContext(context);
            reality.Init();
            return new ContextHarness(context, reality, logic, input, assets);
        }

        public void Dispose()
        {
            Reality.Shutdown();
            _input.Dispose();
            _assets.Dispose();
        }
    }

    private sealed class TestWindow : IWindowController
    {
        public string Title { get; set; } = "Test";
        public Vector2D<int> Size { get; set; } = new(800, 600);
        public Vector2D<int> FramebufferSize => Size;
        public WindowDisplayState DisplayState { get; set; }
        public bool VSync { get; set; }
        public bool IsCloseRequested { get; private set; }

        public void ToggleFullscreen() => DisplayState = WindowDisplayState.Fullscreen;

        public void ToggleBorderlessFullscreen() => DisplayState = WindowDisplayState.BorderlessFullscreen;

        public void RequestClose() => IsCloseRequested = true;
    }
}

[CollectionDefinition("Engine state", DisableParallelization = true)]
public sealed class EngineStateCollection;
