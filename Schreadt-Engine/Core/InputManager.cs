using System.Numerics;
using System.Text;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.Windowing;

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

    private IView? _view;
    private IInputContext? _context;
    private IKeyboard? _keyboard;
    private IMouse? _mouse;
    private bool _disposed;

    public bool Available => _context is not null;

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
        return TryGetBackendCursorMode(mode, out var backendMode) &&
               _mouse?.Cursor.IsSupported(backendMode) == true;
    }

    public void SetCursorMode(InputCursorMode mode)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var cursor = _mouse?.Cursor ?? throw new InvalidOperationException("No mouse is connected.");
        if (!TryGetBackendCursorMode(mode, out var backendMode) || !cursor.IsSupported(backendMode))
            throw new NotSupportedException($"Cursor mode '{mode}' is not supported by the active input backend.");
        cursor.CursorMode = backendMode;
    }

    internal void Initialize(IView view)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(view);
        if (_context is not null) throw new InvalidOperationException("Input has already been initialized.");

        _view = view;
        _context = view.CreateInput();
        _keyboard = _context.Keyboards.FirstOrDefault();
        _mouse = _context.Mice.FirstOrDefault();

        if (_keyboard is not null)
        {
            _keyboard.KeyDown += OnKeyDown;
            _keyboard.KeyUp += OnKeyUp;
            _keyboard.KeyChar += OnKeyChar;
            _keyboard.BeginInput();
        }

        if (_mouse is not null)
        {
            MousePosition = _mouse.Position;
            _mouse.MouseDown += OnMouseDown;
            _mouse.MouseUp += OnMouseUp;
            _mouse.MouseMove += OnMouseMove;
            _mouse.Scroll += OnScroll;
        }
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

        if (_keyboard is not null)
        {
            _keyboard.KeyDown -= OnKeyDown;
            _keyboard.KeyUp -= OnKeyUp;
            _keyboard.KeyChar -= OnKeyChar;
            _keyboard.EndInput();
        }

        if (_mouse is not null)
        {
            _mouse.MouseDown -= OnMouseDown;
            _mouse.MouseUp -= OnMouseUp;
            _mouse.MouseMove -= OnMouseMove;
            _mouse.Scroll -= OnScroll;
        }

        _context?.Dispose();
        _context = null;
        _keyboard = null;
        _mouse = null;
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

    private void OnKeyDown(IKeyboard keyboard, Key key, int scanCode)
    {
        ProcessKeyDown(TranslateKey(key));
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

    private void OnKeyUp(IKeyboard keyboard, Key key, int scanCode)
    {
        ProcessKeyUp(TranslateKey(key));
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

    private void OnKeyChar(IKeyboard keyboard, char character)
    {
        ProcessCharacterTyped(character);
    }

    internal void ProcessCharacterTyped(char character)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _textInput.Append(character);
        CharacterTyped?.Invoke(character);
    }

    private void OnMouseDown(IMouse mouse, MouseButton button)
    {
        ProcessMouseDown(TranslateMouseButton(button));
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

    private void OnMouseUp(IMouse mouse, MouseButton button)
    {
        ProcessMouseUp(TranslateMouseButton(button));
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

    private void OnMouseMove(IMouse mouse, Vector2 position)
    {
        ProcessMouseMove(position);
    }

    internal void ProcessMouseMove(Vector2 position)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var delta = position - MousePosition;
        MousePosition = position;
        MouseDelta += delta;
        MouseMoved?.Invoke(position);
    }

    private void OnScroll(IMouse mouse, ScrollWheel wheel)
    {
        ProcessScroll(new Vector2(wheel.X, wheel.Y));
    }

    internal void ProcessScroll(Vector2 delta)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ScrollDelta += delta;
        Scrolled?.Invoke(delta);
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

    private static InputKey TranslateKey(Key key)
    {
        return Enum.TryParse<InputKey>(key.ToString(), out var inputKey)
            ? inputKey
            : InputKey.Unknown;
    }

    private static InputMouseButton TranslateMouseButton(MouseButton button)
    {
        return Enum.TryParse<InputMouseButton>(button.ToString(), out var inputButton)
            ? inputButton
            : InputMouseButton.Unknown;
    }

    private static bool TryGetBackendCursorMode(InputCursorMode mode, out CursorMode backendMode)
    {
        backendMode = default;
        return Enum.IsDefined(mode) && Enum.TryParse(mode.ToString(), out backendMode);
    }
}
