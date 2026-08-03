using Schreadt_Engine.Component;
using Schreadt_Engine.Core;
using Schreadt_Engine.Gui;
using Silk.NET.Maths;

namespace Example_Game.Logic;

internal static class PlatformerScreens
{
    internal const string PauseScreenName = "pause";
    internal const string VictoryScreenName = "victory";

    internal static GuiScreen CreatePauseScreen(Scene scene)
    {
        var panel = CreateCenteredPanel(new Vector2D<float>(505, 225));
        var heading = panel.AddLabel("ADVENTURE PAUSED");
        heading.Color = new Vector4D<float>(1f, 0.86f, 0.26f, 1f);
        panel.AddLabel("TAKE A BREATH, LITTLE COMET").Scale = 1.4f;
        var resume = panel.AddButton("RESUME");
        var restart = panel.AddButton("RESTART LEVEL");
        var quit = panel.AddButton("QUIT");
        var screen = new GuiScreen(PauseScreenName, panel)
        {
            IsModal = true,
            PausesSimulation = true,
            DismissOnEscape = true
        };
        resume.Clicked += (_, _) => scene.Screens.Remove(screen);
        restart.Clicked += (_, _) =>
        {
            scene.Screens.Remove(screen);
            State.CurrentReality.Scenes.ReloadCurrentScene();
        };
        quit.Clicked += (_, _) => State.Window.RequestClose();
        return screen;
    }

    internal static void ShowVictory(Scene scene, int stars, int deaths)
    {
        if (scene.Screens.Contains(VictoryScreenName)) return;

        var panel = CreateCenteredPanel(new Vector2D<float>(475, 190));
        var heading = panel.AddLabel("YOU REACHED THE SUMMIT!");
        heading.Color = new Vector4D<float>(1f, 0.86f, 0.26f, 1f);
        panel.AddLabel($"STARS: {stars}/3   FALLS: {deaths}").Scale = 1.7f;
        panel.AddLabel("THANKS FOR PLAYING SKYBOUND").Scale = 1.3f;
        var again = panel.AddButton("PLAY AGAIN");
        var quit = panel.AddButton("QUIT");
        var screen = new GuiScreen(VictoryScreenName, panel)
        {
            IsModal = true,
            PausesSimulation = true
        };
        again.Clicked += (_, _) =>
        {
            scene.Screens.Remove(screen);
            State.CurrentReality.Scenes.LoadScene(ExampleGameLogic.LevelOne);
        };
        quit.Clicked += (_, _) => State.Window.RequestClose();
        scene.Screens.Push(screen);
    }

    private static GuiPanel CreateCenteredPanel(Vector2D<float> position) => new()
    {
        Position = position,
        Padding = 18,
        Spacing = 10,
        BackgroundColor = new Vector4D<float>(0.035f, 0.055f, 0.11f, 0.97f)
    };
}
