using System.Numerics;
using System.Text;
using Silk.NET.Maths;
using Silk.NET.SDL;
using SdlApi = Silk.NET.SDL.Sdl;

namespace Schreadt_Engine.Core;

public sealed class InputManager : IInputService, IDisposable
{
    private readonly HashSet<InputKey> _keysDown = [];
    private readonly HashSet<InputKey> _keysPressed = [];
    private readonly HashSet<InputKey> _keysReleased = [];
    private readonly HashSet<InputMouseButton> _mouseButtonsDown = [];
    private readonly HashSet<InputMouseButton> _mouseButtonsPressed = [];
    private readonly HashSet<InputMouseButton> _mouseButtonsReleased = [];
    private readonly Dictionary<string, HashSet<InputBinding>> _actionBindings = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _actionsPressed = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _actionsReleased = new(StringComparer.OrdinalIgnoreCase);
    private readonly StringBuilder _textInput = new();

    private IWindowController? _view;
    private SdlApi? _sdl;
    private bool _disposed;

    public bool Available => _sdl is not null;

    public Vector2 MousePosition { get; private set; }

    public Vector2 MouseDelta { get; private set; }

    public Vector2 ScrollDelta { get; private set; }

    public string TextInput => _textInput.ToString();

    public IReadOnlyCollection<string> Actions => _actionBindings.Keys.ToArray();

    public Vector2D<double> MouseViewportPosition
    {
        get
        {
            var size = _view?.Size ?? default;
            if (size.X <= 0 || size.Y <= 0) return new Vector2D<double>(0.5, 0.5);

            return new Vector2D<double>(
                MousePosition.X / size.X,
                1.0 - MousePosition.Y / size.Y);
        }
    }

    public double ViewportAspectRatio
    {
        get
        {
            var size = _view?.Size ?? default;
            return size.X > 0 && size.Y > 0 ? (double)size.X / size.Y : 1.0;
        }
    }

    public event Action<InputKey>? KeyPressed;
    public event Action<InputKey>? KeyReleased;
    public event Action<char>? CharacterTyped;
    public event Action<InputMouseButton>? MouseButtonPressed;
    public event Action<InputMouseButton>? MouseButtonReleased;
    public event Action<Vector2>? MouseMoved;
    public event Action<Vector2>? Scrolled;

    public bool IsKeyDown(InputKey key) => _keysDown.Contains(key);

    public bool WasKeyPressed(InputKey key) => _keysPressed.Contains(key);

    public bool WasKeyReleased(InputKey key) => _keysReleased.Contains(key);

    public bool IsMouseButtonDown(InputMouseButton button) => _mouseButtonsDown.Contains(button);

    public bool WasMouseButtonPressed(InputMouseButton button) => _mouseButtonsPressed.Contains(button);

    public bool WasMouseButtonReleased(InputMouseButton button) => _mouseButtonsReleased.Contains(button);

    public bool IsActionDown(string action)
    {
        return _actionBindings.TryGetValue(NormalizeAction(action), out var bindings) &&
               IsAnyBindingDown(bindings);
    }

    public bool WasActionPressed(string action) => _actionsPressed.Contains(NormalizeAction(action));

    public bool WasActionReleased(string action) => _actionsReleased.Contains(NormalizeAction(action));

    public IReadOnlyList<InputBinding> GetActionBindings(string action)
    {
        return _actionBindings.TryGetValue(NormalizeAction(action), out var bindings)
            ? bindings.ToArray()
            : [];
    }

    public void SetActionBindings(string action, params InputBinding[] bindings)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var normalizedAction = NormalizeAction(action);
        ArgumentNullException.ThrowIfNull(bindings);
        if (bindings.Length == 0)
            throw new ArgumentException("An action must have at least one binding.", nameof(bindings));
        if (bindings.Any(binding => !binding.IsValid))
            throw new ArgumentException("Action bindings must contain exactly one key or mouse button.", nameof(bindings));

        _actionBindings[normalizedAction] = bindings.ToHashSet();
        _actionsPressed.Remove(normalizedAction);
        _actionsReleased.Remove(normalizedAction);
    }

    public bool RemoveAction(string action)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var normalizedAction = NormalizeAction(action);
        _actionsPressed.Remove(normalizedAction);
        _actionsReleased.Remove(normalizedAction);
        return _actionBindings.Remove(normalizedAction);
    }

    public bool IsCursorModeSupported(InputCursorMode mode)
    {
        return Available && Enum.IsDefined(mode);
    }

    public void SetCursorMode(InputCursorMode mode)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        var sdl = _sdl ?? throw new InvalidOperationException("SDL input has not been initialized.");

        var relative = mode is InputCursorMode.Disabled or InputCursorMode.Raw;
        if (mode == InputCursorMode.Raw) sdl.SetHint(SdlApi.HintMouseRelativeModeWarp, "0");
        if (sdl.SetRelativeMouseMode(relative ? SdlBool.True : SdlBool.False) != 0)
            throw new InvalidOperationException($"SDL could not change relative mouse mode: {sdl.GetErrorS()}");

        var showCursor = mode == InputCursorMode.Normal;
        if (sdl.ShowCursor(showCursor ? SdlApi.Enable : SdlApi.Disable) < 0)
            throw new InvalidOperationException($"SDL could not change cursor visibility: {sdl.GetErrorS()}");
    }

    internal void Initialize(SdlApi sdl, IWindowController view)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(sdl);
        ArgumentNullException.ThrowIfNull(view);
        if (_sdl is not null) throw new InvalidOperationException("Input has already been initialized.");

        _sdl = sdl;
        _view = view;
        var mouseX = 0;
        var mouseY = 0;
        sdl.GetMouseState(ref mouseX, ref mouseY);
        MousePosition = new Vector2(mouseX, mouseY);
    }

    internal void EndFrame()
    {
        _keysPressed.Clear();
        _keysReleased.Clear();
        _mouseButtonsPressed.Clear();
        _mouseButtonsReleased.Clear();
        _actionsPressed.Clear();
        _actionsReleased.Clear();
        _textInput.Clear();
        MouseDelta = Vector2.Zero;
        ScrollDelta = Vector2.Zero;
    }

    public void Dispose()
    {
        if (_disposed) return;

        _sdl = null;
        _view = null;
        _keysDown.Clear();
        _keysPressed.Clear();
        _keysReleased.Clear();
        _mouseButtonsDown.Clear();
        _mouseButtonsPressed.Clear();
        _mouseButtonsReleased.Clear();
        _actionsPressed.Clear();
        _actionsReleased.Clear();
        _actionBindings.Clear();
        _textInput.Clear();
        MousePosition = Vector2.Zero;
        MouseDelta = Vector2.Zero;
        ScrollDelta = Vector2.Zero;
        _disposed = true;
    }

    internal void ProcessKeyDown(InputKey inputKey)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (inputKey == InputKey.Unknown || _keysDown.Contains(inputKey)) return;

        var affectedActions = CaptureAffectedActionStates(InputBinding.ForKey(inputKey));
        _keysDown.Add(inputKey);
        _keysPressed.Add(inputKey);
        UpdateActionTransitions(affectedActions);
        KeyPressed?.Invoke(inputKey);
    }

    internal void ProcessKeyUp(InputKey inputKey)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (inputKey == InputKey.Unknown) return;

        var affectedActions = CaptureAffectedActionStates(InputBinding.ForKey(inputKey));
        _keysDown.Remove(inputKey);
        _keysReleased.Add(inputKey);
        UpdateActionTransitions(affectedActions);
        KeyReleased?.Invoke(inputKey);
    }

    internal void ProcessCharacterTyped(char character)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _textInput.Append(character);
        CharacterTyped?.Invoke(character);
    }

    internal void ProcessMouseDown(InputMouseButton inputButton)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (inputButton == InputMouseButton.Unknown || _mouseButtonsDown.Contains(inputButton)) return;

        var affectedActions = CaptureAffectedActionStates(InputBinding.ForMouseButton(inputButton));
        _mouseButtonsDown.Add(inputButton);
        _mouseButtonsPressed.Add(inputButton);
        UpdateActionTransitions(affectedActions);
        MouseButtonPressed?.Invoke(inputButton);
    }

    internal void ProcessMouseUp(InputMouseButton inputButton)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (inputButton == InputMouseButton.Unknown) return;

        var affectedActions = CaptureAffectedActionStates(InputBinding.ForMouseButton(inputButton));
        _mouseButtonsDown.Remove(inputButton);
        _mouseButtonsReleased.Add(inputButton);
        UpdateActionTransitions(affectedActions);
        MouseButtonReleased?.Invoke(inputButton);
    }

    internal void ProcessMouseMove(Vector2 position)
    {
        ProcessMouseMove(position, position - MousePosition);
    }

    internal void ProcessMouseMove(Vector2 position, Vector2 delta)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        MousePosition = position;
        MouseDelta += delta;
        MouseMoved?.Invoke(position);
    }

    internal void ProcessScroll(Vector2 delta)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ScrollDelta += delta;
        Scrolled?.Invoke(delta);
    }

    internal void ProcessFocusLost()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        foreach (var key in _keysDown.ToArray()) ProcessKeyUp(key);
        foreach (var button in _mouseButtonsDown.ToArray()) ProcessMouseUp(button);
    }

    private KeyValuePair<string, bool>[] CaptureAffectedActionStates(InputBinding binding)
    {
        return _actionBindings
            .Where(pair => pair.Value.Contains(binding))
            .Select(pair => new KeyValuePair<string, bool>(pair.Key, IsAnyBindingDown(pair.Value)))
            .ToArray();
    }

    private void UpdateActionTransitions(IEnumerable<KeyValuePair<string, bool>> previousStates)
    {
        foreach (var (action, wasDown) in previousStates)
        {
            var isDown = IsAnyBindingDown(_actionBindings[action]);
            if (!wasDown && isDown) _actionsPressed.Add(action);
            if (wasDown && !isDown) _actionsReleased.Add(action);
        }
    }

    private bool IsAnyBindingDown(IEnumerable<InputBinding> bindings)
    {
        return bindings.Any(binding =>
            (binding.Key is { } key && _keysDown.Contains(key)) ||
            (binding.MouseButton is { } button && _mouseButtonsDown.Contains(button)));
    }

    private static string NormalizeAction(string action)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        return action.Trim();
    }

    internal static InputKey TranslateScancode(Scancode scancode)
    {
        var name = scancode.ToString();
        if (!name.StartsWith("Scancode", StringComparison.Ordinal)) return InputKey.Unknown;
        var keyName = name["Scancode".Length..];

        var translatedName = keyName switch
        {
            "0" => nameof(InputKey.Number0),
            "1" => nameof(InputKey.Number1),
            "2" => nameof(InputKey.Number2),
            "3" => nameof(InputKey.Number3),
            "4" => nameof(InputKey.Number4),
            "5" => nameof(InputKey.Number5),
            "6" => nameof(InputKey.Number6),
            "7" => nameof(InputKey.Number7),
            "8" => nameof(InputKey.Number8),
            "9" => nameof(InputKey.Number9),
            "Return" => nameof(InputKey.Enter),
            "Equals" => nameof(InputKey.Equal),
            "Grave" => nameof(InputKey.GraveAccent),
            "Numlockclear" => nameof(InputKey.NumLock),
            "KP0" => nameof(InputKey.Keypad0),
            "KP1" => nameof(InputKey.Keypad1),
            "KP2" => nameof(InputKey.Keypad2),
            "KP3" => nameof(InputKey.Keypad3),
            "KP4" => nameof(InputKey.Keypad4),
            "KP5" => nameof(InputKey.Keypad5),
            "KP6" => nameof(InputKey.Keypad6),
            "KP7" => nameof(InputKey.Keypad7),
            "KP8" => nameof(InputKey.Keypad8),
            "KP9" => nameof(InputKey.Keypad9),
            "KPPeriod" => nameof(InputKey.KeypadDecimal),
            "KPDivide" => nameof(InputKey.KeypadDivide),
            "KPMultiply" => nameof(InputKey.KeypadMultiply),
            "KPMinus" => nameof(InputKey.KeypadSubtract),
            "KPPlus" => nameof(InputKey.KeypadAdd),
            "KPEnter" => nameof(InputKey.KeypadEnter),
            "KPEquals" => nameof(InputKey.KeypadEqual),
            "Lshift" => nameof(InputKey.ShiftLeft),
            "Lctrl" => nameof(InputKey.ControlLeft),
            "Lalt" => nameof(InputKey.AltLeft),
            "Lgui" => nameof(InputKey.SuperLeft),
            "Rshift" => nameof(InputKey.ShiftRight),
            "Rctrl" => nameof(InputKey.ControlRight),
            "Ralt" => nameof(InputKey.AltRight),
            "Rgui" => nameof(InputKey.SuperRight),
            "Application" => nameof(InputKey.Menu),
            _ => keyName
        };

        return Enum.TryParse<InputKey>(translatedName, true, out var inputKey)
            ? inputKey
            : InputKey.Unknown;
    }

    internal static InputMouseButton TranslateMouseButton(byte button)
    {
        return button switch
        {
            SdlApi.ButtonLeft => InputMouseButton.Left,
            SdlApi.ButtonMiddle => InputMouseButton.Middle,
            SdlApi.ButtonRight => InputMouseButton.Right,
            SdlApi.ButtonX1 => InputMouseButton.Button4,
            SdlApi.ButtonX2 => InputMouseButton.Button5,
            6 => InputMouseButton.Button6,
            7 => InputMouseButton.Button7,
            8 => InputMouseButton.Button8,
            9 => InputMouseButton.Button9,
            10 => InputMouseButton.Button10,
            11 => InputMouseButton.Button11,
            12 => InputMouseButton.Button12,
            _ => InputMouseButton.Unknown
        };
    }
}
