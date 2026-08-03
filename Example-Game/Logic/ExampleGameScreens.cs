using Schreadt_Engine.Component;
using Schreadt_Engine.Core;
using Schreadt_Engine.Gui;
using Silk.NET.Maths;

namespace Example_Game.Logic;

internal static class ExampleGameScreens
{
    internal const string PauseScreenName = "pause";
    private const string GameOverScreenName = "game-over";

    internal static void AddSceneHud(Scene scene, string sceneName)
    {
        var hud = scene.Gui.AddPanel();
        hud.Position = new Vector2D<float>(1010.0f, 12.0f);
        hud.AddLabel($"SCENE: {sceneName}");
        var gameOverButton = hud.AddButton("SHOW GAME OVER");
        gameOverButton.Clicked += (_, _) => ShowGameOver(scene);
    }

    internal static GuiScreen CreatePauseScreen(Scene scene)
    {
        var panel = new GuiPanel
        {
            Position = new Vector2D<float>(500.0f, 220.0f),
            Padding = 16.0f,
            Spacing = 10.0f,
            BackgroundColor = new Vector4D<float>(0.025f, 0.035f, 0.06f, 0.96f)
        };
        panel.AddLabel("GAME PAUSED");
        var resumeButton = panel.AddButton("RESUME");
        var restartButton = panel.AddButton("RESTART SCENE");
        var quitButton = panel.AddButton("QUIT");
        var screen = new GuiScreen(PauseScreenName, panel)
        {
            IsModal = true,
            PausesSimulation = true,
            DismissOnEscape = true
        };

        resumeButton.Clicked += (_, _) => scene.Screens.Remove(screen);
        restartButton.Clicked += (_, _) =>
        {
            scene.Screens.Remove(screen);
            State.CurrentReality.Scenes.ReloadCurrentScene();
        };
        quitButton.Clicked += (_, _) => State.Window.RequestClose();
        return screen;
    }

    private static void ShowGameOver(Scene scene)
    {
        if (scene.Screens.Contains(GameOverScreenName)) return;

        var panel = new GuiPanel
        {
            Position = new Vector2D<float>(490.0f, 240.0f),
            Padding = 16.0f,
            Spacing = 10.0f,
            BackgroundColor = new Vector4D<float>(0.12f, 0.025f, 0.045f, 0.96f)
        };
        panel.AddLabel("GAME OVER");
        var continueButton = panel.AddButton("CONTINUE DEMO");
        var screen = new GuiScreen(GameOverScreenName, panel)
        {
            IsModal = true,
            PausesSimulation = true
        };
        continueButton.Clicked += (_, _) => scene.Screens.Remove(screen);
        scene.Screens.Push(screen);
    }
}
