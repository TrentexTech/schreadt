using Example_Game.Logic;
using Schreadt_Engine.Component.Logic;
using Schreadt_Engine.Core;
using Schreadt_Engine.Gui;

namespace Schreadt_Engine.Tests.Examples;

public sealed class LevelSelectorTests
{
    [Fact]
    public void LevelSelector_ListsLevelsTransitionsAndTracksTheCurrentLevel()
    {
        var gui = new GuiSystem();
        var runtime = new RuntimeController();
        var scenes = new SceneManager(gui, runtime);
        scenes.RegisterScene(ExampleGameLogic.LevelOne, () => new EmptySceneLogic());
        scenes.RegisterScene(ExampleGameLogic.LevelTwo, () => new EmptySceneLogic());
        scenes.RegisterScene(ExampleGameLogic.LevelThree, () => new EmptySceneLogic());
        scenes.RegisterScene(ExampleGameLogic.LevelFour, () => new EmptySceneLogic());
        scenes.RegisterScene(ExampleGameLogic.LevelFive, () => new EmptySceneLogic());
        scenes.RegisterScene(ExampleGameLogic.LevelSix, () => new EmptySceneLogic());
        scenes.LoadScene(ExampleGameLogic.LevelOne);
        var selector = new ExampleLevelSelector(scenes, gui);

        try
        {
            scenes.Init();
            selector.Update();

            Assert.Same(selector.Root, Assert.Single(gui.Elements));
            Assert.Equal(ExampleGameLogic.LevelCount, selector.Buttons.Count);
            Assert.Contains(ExampleGameLogic.LevelSix, selector.Buttons.Keys);
            Assert.Equal("CURRENT: SUNNY MEADOWS", selector.CurrentLevel.Text);
            Assert.False(selector.Buttons[ExampleGameLogic.LevelOne].Enabled);
            Assert.All(
                selector.Buttons.Where(entry => entry.Key != ExampleGameLogic.LevelOne),
                entry => Assert.True(entry.Value.Enabled));

            Assert.True(selector.SelectLevel(ExampleGameLogic.LevelThree));
            Assert.True(scenes.HasPendingSceneLoad);
            Assert.All(selector.Buttons.Values, button => Assert.False(button.Enabled));

            scenes.ProcessPendingSceneChange(0.0);
            Assert.True(scenes.IsTransitioning);
            Assert.Equal(ExampleGameLogic.LevelThree, scenes.TransitionTargetSceneName);
            scenes.ProcessPendingSceneChange(ExampleGameLogic.LevelTransition.PhaseDuration);
            Assert.Equal(ExampleGameLogic.LevelThree, scenes.CurrentSceneName);
            Assert.Equal("CURRENT: LUNAR GARDENS", selector.CurrentLevel.Text);
            Assert.All(selector.Buttons.Values, button => Assert.False(button.Enabled));

            scenes.ProcessPendingSceneChange(ExampleGameLogic.LevelTransition.PhaseDuration);
            scenes.CompleteTransitionFrame();
            selector.Update();
            Assert.False(selector.Buttons[ExampleGameLogic.LevelThree].Enabled);
            Assert.True(selector.Buttons[ExampleGameLogic.LevelOne].Enabled);

            scenes.CurrentScene!.Screens.Push(new GuiScreen("selector-modal", new GuiPanel())
            {
                IsModal = true
            });
            selector.Update();
            Assert.All(selector.Buttons.Values, button => Assert.False(button.Enabled));
        }
        finally
        {
            scenes.Shutdown();
            selector.Dispose();
        }

        Assert.Empty(gui.Elements);
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
