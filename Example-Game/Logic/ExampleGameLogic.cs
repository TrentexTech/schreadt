using Example_Game.Logic.scenes;
using Schreadt_Engine.Asset;
using Schreadt_Engine.Component.Logic;
using Schreadt_Engine.Core;

namespace Example_Game.Logic;

public class ExampleGameLogic : GameLogic
{
    private const string MainScene = "main";
    private const string AlternateScene = "alternate";
    private const double DefaultOrthographicSize = 1.25;
    private const double MinimumOrthographicSize = 0.25;
    private const double MaximumOrthographicSize = 8.0;
    private const double ZoomFactorPerScrollStep = 0.85;
    private readonly IInputService? _input;

    private IInputService Input => _input ?? State.Input;

    public ExampleGameLogic(IInputService? input = null)
    {
        _input = input;
    }

    public override void Update(double dt)
    {
        if (Input.WasActionPressed(ExampleInputActions.SwitchScene))
        {
            SwitchScene();
        }

        var scroll = Input.ScrollDelta.Y;
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
        Input.SetActionBindings(
            ExampleInputActions.MoveUp,
            InputBinding.ForKey(InputKey.W),
            InputBinding.ForKey(InputKey.Up));
        Input.SetActionBindings(
            ExampleInputActions.MoveDown,
            InputBinding.ForKey(InputKey.S),
            InputBinding.ForKey(InputKey.Down));
        Input.SetActionBindings(
            ExampleInputActions.MoveLeft,
            InputBinding.ForKey(InputKey.A),
            InputBinding.ForKey(InputKey.Left));
        Input.SetActionBindings(
            ExampleInputActions.MoveRight,
            InputBinding.ForKey(InputKey.D),
            InputBinding.ForKey(InputKey.Right));
        Input.SetActionBindings(
            ExampleInputActions.MoveToPointer,
            InputBinding.ForMouseButton(InputMouseButton.Left));
        Input.SetActionBindings(
            ExampleInputActions.SwitchScene,
            InputBinding.ForKey(InputKey.Tab));

        State.Assets.RegisterDecoder(new JsonAssetDecoder<ExamplePhysicsTuning>());
        var physicsTuning = State.Assets.Get<ExamplePhysicsTuning>("example/physics-tuning");
        Reality.Scenes.RegisterScene(MainScene, () => new Scene0(physicsTuning, Input));
        Reality.Scenes.RegisterScene(AlternateScene, () => new Scene1(physicsTuning, Input));
        Reality.Scenes.LoadScene(MainScene);
        Reality.MainCamera.OrthographicSize = DefaultOrthographicSize;

        var switchSceneButton = State.Gui.AddButton("SWITCH SCENE");
        switchSceneButton.Position = new Silk.NET.Maths.Vector2D<float>(12.0f, 66.0f);
        switchSceneButton.Clicked += (_, _) => SwitchScene();
    }

    private void SwitchScene()
    {
        var nextScene = Reality.Scenes.CurrentSceneName == MainScene
            ? AlternateScene
            : MainScene;
        Reality.Scenes.LoadScene(nextScene);
    }
}
