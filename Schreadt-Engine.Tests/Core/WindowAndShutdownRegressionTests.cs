using Schreadt_Engine.Component.Logic;
using Schreadt_Engine.Core;

namespace Schreadt_Engine.Tests.Core;

public sealed class WindowAndShutdownRegressionTests
{
    [Fact]
    public void FullscreenToggle_AlternatesUsingRequestedState()
    {
        var firstToggle = Window.GetFullscreenToggleTarget(
            WindowDisplayState.Normal,
            WindowDisplayState.Normal,
            WindowDisplayState.Fullscreen);
        var secondToggle = Window.GetFullscreenToggleTarget(
            firstToggle,
            WindowDisplayState.Normal,
            WindowDisplayState.Fullscreen);

        Assert.Equal(WindowDisplayState.Fullscreen, firstToggle);
        Assert.Equal(WindowDisplayState.Normal, secondToggle);
    }

    [Fact]
    public void BorderlessFullscreenToggle_AlternatesAndCanReplaceExclusiveFullscreen()
    {
        var borderless = Window.GetFullscreenToggleTarget(
            WindowDisplayState.Normal,
            WindowDisplayState.Normal,
            WindowDisplayState.BorderlessFullscreen);
        var exclusive = Window.GetFullscreenToggleTarget(
            borderless,
            WindowDisplayState.Normal,
            WindowDisplayState.Fullscreen);
        var borderlessAgain = Window.GetFullscreenToggleTarget(
            exclusive,
            WindowDisplayState.Normal,
            WindowDisplayState.BorderlessFullscreen);
        var windowed = Window.GetFullscreenToggleTarget(
            borderlessAgain,
            WindowDisplayState.Normal,
            WindowDisplayState.BorderlessFullscreen);

        Assert.Equal(WindowDisplayState.BorderlessFullscreen, borderless);
        Assert.Equal(WindowDisplayState.Fullscreen, exclusive);
        Assert.Equal(WindowDisplayState.BorderlessFullscreen, borderlessAgain);
        Assert.Equal(WindowDisplayState.Normal, windowed);
    }

    [Fact]
    public void SceneManager_ShutdownLeavesManagerUninitializedInsteadOfNullSceneState()
    {
        var scenes = new SceneManager();
        scenes.RegisterScene("test", () => new EmptySceneLogic());
        scenes.LoadScene("test");
        scenes.Init();

        scenes.Shutdown();
        scenes.Shutdown();

        var exception = Assert.Throws<InvalidOperationException>(() => scenes.Update(0.0));
        Assert.Contains("not been initialized", exception.Message);
    }

    private sealed class EmptySceneLogic : SceneLogic
    {
        public override void Init()
        {
        }

        public override void Update(double dt)
        {
        }
    }
}
