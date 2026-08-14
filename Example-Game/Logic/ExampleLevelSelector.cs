using Schreadt_Engine.Component;
using Schreadt_Engine.Core;
using Schreadt_Engine.Gui;
using Silk.NET.Maths;

namespace Example_Game.Logic;

internal sealed class ExampleLevelSelector : IDisposable
{
    private static readonly LevelChoice[] LevelChoices =
    [
        new(ExampleGameLogic.LevelOne, "1  SUNNY MEADOWS"),
        new(ExampleGameLogic.LevelTwo, "2  CRYSTAL HEIGHTS"),
        new(ExampleGameLogic.LevelThree, "3  LUNAR GARDENS"),
        new(ExampleGameLogic.LevelFour, "4  CLOCKWORK FORTRESS"),
        new(ExampleGameLogic.LevelFive, "5  TEMPEST SPIRE"),
        new(ExampleGameLogic.LevelSix, "6  ORIENTED COLLIDER LAB")
    ];

    private readonly SceneManager _scenes;
    private readonly GuiSystem _gui;
    private readonly Dictionary<string, GuiButton> _buttons = new(StringComparer.Ordinal);
    private bool _selectionPending;
    private bool _disposed;

    internal ExampleLevelSelector(SceneManager scenes, GuiSystem gui)
    {
        ArgumentNullException.ThrowIfNull(scenes);
        ArgumentNullException.ThrowIfNull(gui);
        _scenes = scenes;
        _gui = gui;

        Root = new GuiPanel
        {
            Position = new Vector2D<float>(12.0f, 145.0f),
            Padding = 7.0f,
            Spacing = 4.0f,
            BackgroundColor = new Vector4D<float>(0.035f, 0.055f, 0.11f, 0.9f)
        };
        var heading = Root.AddLabel("LEVEL SELECT");
        heading.Scale = 1.4f;
        heading.Color = new Vector4D<float>(0.38f, 0.9f, 1.0f, 1.0f);
        CurrentLevel = Root.AddLabel("CURRENT: LOADING");
        CurrentLevel.Scale = 1.15f;
        CurrentLevel.Color = new Vector4D<float>(1.0f, 0.84f, 0.28f, 1.0f);

        foreach (var choice in LevelChoices)
        {
            var button = Root.AddButton(choice.ButtonText);
            button.Scale = 1.15f;
            button.Padding = 4.0f;
            button.Clicked += (_, _) => SelectLevel(choice.SceneName);
            _buttons.Add(choice.SceneName, button);
        }

        _gui.Add(Root);
        _scenes.SceneLoaded += HandleSceneLoaded;
        if (_scenes.CurrentScene is { } currentScene) HandleSceneLoaded(currentScene);
        else RefreshState();
    }

    internal GuiPanel Root { get; }
    internal GuiLabel CurrentLevel { get; }
    internal IReadOnlyDictionary<string, GuiButton> Buttons => _buttons;

    internal void Update() => RefreshState();

    internal bool SelectLevel(string sceneName)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(sceneName);
        if (!_buttons.ContainsKey(sceneName))
            throw new ArgumentException($"'{sceneName}' is not an Example Game level.", nameof(sceneName));
        if (IsInteractionBlocked() || string.Equals(_scenes.CurrentSceneName, sceneName, StringComparison.Ordinal))
            return false;

        _selectionPending = true;
        RefreshState();
        try
        {
            _scenes.LoadScene(sceneName, ExampleGameLogic.LevelTransition);
            return true;
        }
        catch
        {
            _selectionPending = false;
            RefreshState();
            throw;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _scenes.SceneLoaded -= HandleSceneLoaded;
        _gui.Remove(Root);
    }

    private void HandleSceneLoaded(Scene scene)
    {
        _selectionPending = false;
        CurrentLevel.Text = $"CURRENT: {GetDisplayName(scene.Name)}";
        RefreshState();
    }

    private void RefreshState()
    {
        var blocked = IsInteractionBlocked();
        foreach (var (sceneName, button) in _buttons)
        {
            button.Enabled = !blocked &&
                             !string.Equals(sceneName, _scenes.CurrentSceneName, StringComparison.Ordinal);
        }
    }

    private bool IsInteractionBlocked()
    {
        var screens = _scenes.CurrentScene?.Screens;
        return _selectionPending ||
               _scenes.HasPendingSceneLoad ||
               _scenes.IsTransitioning ||
               screens?.IsTransitioning == true ||
               screens?.Top?.IsModal == true;
    }

    private static string GetDisplayName(string sceneName) => LevelChoices
        .FirstOrDefault(choice => string.Equals(choice.SceneName, sceneName, StringComparison.Ordinal))
        ?.ButtonText[3..] ?? sceneName.ToUpperInvariant();

    private sealed record LevelChoice(string SceneName, string ButtonText);
}
