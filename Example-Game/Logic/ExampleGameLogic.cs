using Example_Game.Logic.scenes;
using Schreadt_Engine.Asset;
using Schreadt_Engine.Component.Logic;
using Schreadt_Engine.Core;
using Silk.NET.Maths;

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

        var controls = State.Gui.AddPanel();
        controls.Position = new Vector2D<float>(12.0f, 66.0f);
        controls.Padding = 4.0f;
        controls.Spacing = 4.0f;

        var switchSceneButton = controls.AddButton("SWITCH SCENE");
        switchSceneButton.Clicked += (_, _) => SwitchScene();

        var pauseButton = controls.AddButton("PAUSE");
        var stepButton = controls.AddButton("STEP ONE FRAME");
        stepButton.Enabled = false;
        pauseButton.Clicked += (_, _) => State.Runtime.TogglePause();
        State.Runtime.PauseStateChanged += paused =>
        {
            pauseButton.Text = paused ? "RESUME" : "PAUSE";
            stepButton.Enabled = paused;
        };
        stepButton.Clicked += (_, _) => State.Runtime.StepOneFrame();

        double[] timeScales = [0.5, 1.0, 2.0];
        var timeScaleIndex = 1;
        var timeScaleButton = controls.AddButton("TIME SCALE: 1.0X");
        timeScaleButton.Clicked += (_, _) =>
        {
            timeScaleIndex = (timeScaleIndex + 1) % timeScales.Length;
            State.Runtime.TimeScale = timeScales[timeScaleIndex];
            timeScaleButton.Text = FormattableString.Invariant($"TIME SCALE: {State.Runtime.TimeScale:F1}X");
        };

        var fullscreenButton = controls.AddButton("TOGGLE FULLSCREEN");
        fullscreenButton.Clicked += (_, _) => State.Window.ToggleFullscreen();

        var borderlessFullscreenButton = controls.AddButton("TOGGLE BORDERLESS");
        borderlessFullscreenButton.Clicked += (_, _) => State.Window.ToggleBorderlessFullscreen();

        var vsyncButton = controls.AddButton(State.Window.VSync ? "VSYNC: ON" : "VSYNC: OFF");
        vsyncButton.Clicked += (_, _) =>
        {
            State.Window.VSync = !State.Window.VSync;
            vsyncButton.Text = State.Window.VSync ? "VSYNC: ON" : "VSYNC: OFF";
        };

        var quitButton = controls.AddButton("QUIT");
        quitButton.Clicked += (_, _) => State.Window.RequestClose();
    }

    private void SwitchScene()
    {
        var nextScene = Reality.Scenes.CurrentSceneName == MainScene
            ? AlternateScene
            : MainScene;
        Reality.Scenes.LoadScene(nextScene);
    }
}
