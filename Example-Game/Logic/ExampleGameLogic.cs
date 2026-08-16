using Example_Game.Logic.scenes;
using Schreadt_Engine.Animation.Tweening;
using Schreadt_Engine.Component.Logic;
using Schreadt_Engine.Core;
using Schreadt_Engine.Gui;
using Silk.NET.Maths;

namespace Example_Game.Logic;

public sealed class ExampleGameLogic : GameLogic
{
    internal const string LevelOne = "sunny-meadows";
    internal const string LevelTwo = "crystal-heights";
    internal const string LevelThree = "lunar-gardens";
    internal const string LevelFour = "clockwork-fortress";
    internal const string LevelFive = "tempest-spire";
    internal const string LevelSix = "oriented-collider-lab";
    internal const int LevelCount = 6;
    internal static SceneTransition LevelTransition { get; } = new FadeToColorSceneTransition(
        new Vector4D<float>(0.01f, 0.02f, 0.06f, 1.0f),
        0.55,
        TweenEasings.SineInOut);
    private readonly IInputService? _inputOverride;
    private ExampleLevelSelector? _levelSelector;

    private IInputService Input => _inputOverride ?? Context.Input;

    public ExampleGameLogic(IInputService? input = null)
    {
        _inputOverride = input;
    }

    public override void Init()
    {
        Input.SetActionBindings(ExampleInputActions.MoveLeft,
            InputBinding.ForKey(InputKey.A), InputBinding.ForKey(InputKey.Left));
        Input.SetActionBindings(ExampleInputActions.MoveRight,
            InputBinding.ForKey(InputKey.D), InputBinding.ForKey(InputKey.Right));
        Input.SetActionBindings(ExampleInputActions.Jump,
            InputBinding.ForKey(InputKey.Space), InputBinding.ForKey(InputKey.W), InputBinding.ForKey(InputKey.Up));
        Input.SetActionBindings(ExampleInputActions.Interact, InputBinding.ForKey(InputKey.E));
        Input.SetActionBindings(ExampleInputActions.Restart, InputBinding.ForKey(InputKey.R));
        Input.SetActionBindings(ExampleInputActions.Pause, InputBinding.ForKey(InputKey.P));

        Reality.Scenes.RegisterScene(LevelOne, () => new Scene0(Input));
        Reality.Scenes.RegisterScene(LevelTwo, () => new Scene1(Input));
        Reality.Scenes.RegisterScene(LevelThree, () => new Scene2(Input));
        Reality.Scenes.RegisterScene(LevelFour, () => new Scene3(Input));
        Reality.Scenes.RegisterScene(LevelFive, () => new Scene4(Input));
        Reality.Scenes.RegisterScene(LevelSix, () => new Scene5(Input));
        Reality.Scenes.LoadScene(LevelOne);
        Reality.MainCamera.OrthographicSize = 2.4;

        var help = Context.Gui.AddPanel();
        help.Position = new Vector2D<float>(12, 12);
        help.Padding = 7;
        help.Spacing = 5;
        help.BackgroundColor = new Vector4D<float>(0.035f, 0.055f, 0.11f, 0.9f);
        var title = help.AddLabel("SKYBOUND");
        title.Color = new Vector4D<float>(1f, 0.86f, 0.26f, 1f);
        help.AddLabel("A/D OR ARROWS: MOVE\nSPACE/W/UP: JUMP   E: INTERACT\nR: RESTART   P: PAUSE").Scale = 1.25f;
        help.AddButton("PAUSE").Clicked += (_, _) => TogglePause();
        _levelSelector = new ExampleLevelSelector(Reality.Scenes, Context.Gui);
    }

    public override void Update(double dt)
    {
        if (Input.WasActionPressed(ExampleInputActions.Restart))
        {
            Reality.Scene.Screens.Clear();
            Reality.Scenes.ReloadCurrentScene(LevelTransition);
        }

        if (Input.WasActionPressed(ExampleInputActions.Pause)) TogglePause();
        _levelSelector?.Update();
    }

    public override void Shutdown()
    {
        _levelSelector?.Dispose();
        _levelSelector = null;
    }

    private void TogglePause()
    {
        var scene = Reality.Scene;
        if (scene.Screens.IsTransitioning) return;
        if (scene.Screens.Top?.Name == PlatformerScreens.PauseScreenName)
        {
            scene.Screens.Pop();
            return;
        }

        if (!scene.Screens.Contains(PlatformerScreens.VictoryScreenName))
            scene.Screens.Push(PlatformerScreens.CreatePauseScreen(scene));
    }
}
