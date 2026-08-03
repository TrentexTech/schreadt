using Example_Game.Logic.scenes;
using Schreadt_Engine.Component.Logic;
using Schreadt_Engine.Core;
using Silk.NET.Input;

namespace Example_Game.Logic;

public class ExampleGameLogic : GameLogic
{
    private const string MainScene = "main";
    private const string AlternateScene = "alternate";
    private const double DefaultOrthographicSize = 1.25;
    private const double MinimumOrthographicSize = 0.25;
    private const double MaximumOrthographicSize = 8.0;
    private const double ZoomFactorPerScrollStep = 0.85;

    public override void Update(double dt)
    {
        if (State.Input.WasKeyPressed(Key.Tab))
        {
            var nextScene = Reality.Scenes.CurrentSceneName == MainScene
                ? AlternateScene
                : MainScene;
            Reality.Scenes.LoadScene(nextScene);
        }

        var scroll = State.Input.ScrollDelta.Y;
        if (scroll == 0)
        {
            return;
        }

        var camera = Reality.MainCamera;
        var requestedSize = camera.OrthographicSize * Math.Pow(ZoomFactorPerScrollStep, scroll);
        camera.OrthographicSize = Math.Clamp(
            requestedSize,
            MinimumOrthographicSize,
            MaximumOrthographicSize);
    }

    public override void Init()
    {
        var physicsTuning = State.Assets.GetJson<ExamplePhysicsTuning>("example/physics-tuning");
        Reality.Scenes.RegisterScene(MainScene, () => new Scene0(physicsTuning));
        Reality.Scenes.RegisterScene(AlternateScene, () => new Scene1(physicsTuning));
        Reality.Scenes.LoadScene(MainScene);
        Reality.MainCamera.OrthographicSize = DefaultOrthographicSize;
    }
}
