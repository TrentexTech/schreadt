using System.Numerics;
using Silk.NET.Maths;

namespace Schreadt_Engine.Core;

public enum InputKey
{
    Unknown,
    Space,
    Apostrophe,
    Comma,
    Minus,
    Period,
    Slash,
    Number0,
    Number1,
    Number2,
    Number3,
    Number4,
    Number5,
    Number6,
    Number7,
    Number8,
    Number9,
    Semicolon,
    Equal,
    A,
    B,
    C,
    D,
    E,
    F,
    G,
    H,
    I,
    J,
    K,
    L,
    M,
    N,
    O,
    P,
    Q,
    R,
    S,
    T,
    U,
    V,
    W,
    X,
    Y,
    Z,
    LeftBracket,
    BackSlash,
    RightBracket,
    GraveAccent,
    Escape,
    Enter,
    Tab,
    Backspace,
    Insert,
    Delete,
    Right,
    Left,
    Down,
    Up,
    PageUp,
    PageDown,
    Home,
    End,
    CapsLock,
    ScrollLock,
    NumLock,
    PrintScreen,
    Pause,
    F1,
    F2,
    F3,
    F4,
    F5,
    F6,
    F7,
    F8,
    F9,
    F10,
    F11,
    F12,
    F13,
    F14,
    F15,
    F16,
    F17,
    F18,
    F19,
    F20,
    F21,
    F22,
    F23,
    F24,
    F25,
    Keypad0,
    Keypad1,
    Keypad2,
    Keypad3,
    Keypad4,
    Keypad5,
    Keypad6,
    Keypad7,
    Keypad8,
    Keypad9,
    KeypadDecimal,
    KeypadDivide,
    KeypadMultiply,
    KeypadSubtract,
    KeypadAdd,
    KeypadEnter,
    KeypadEqual,
    ShiftLeft,
    ControlLeft,
    AltLeft,
    SuperLeft,
    ShiftRight,
    ControlRight,
    AltRight,
    SuperRight,
    Menu
}

public enum InputMouseButton
{
    Unknown,
    Left,
    Right,
    Middle,
    Button4,
    Button5,
    Button6,
    Button7,
    Button8,
    Button9,
    Button10,
    Button11,
    Button12
}

public enum InputCursorMode
{
    Normal,
    Hidden,
    Disabled,
    Raw
}

public readonly record struct InputBinding
{
    public InputKey? Key { get; }
    public InputMouseButton? MouseButton { get; }

    private InputBinding(InputKey? key, InputMouseButton? mouseButton)
    {
        Key = key;
        MouseButton = mouseButton;
    }

    public static InputBinding ForKey(InputKey key)
    {
        if (key == InputKey.Unknown) throw new ArgumentOutOfRangeException(nameof(key));
        return new InputBinding(key, null);
    }

    public static InputBinding ForMouseButton(InputMouseButton button)
    {
        if (button == InputMouseButton.Unknown) throw new ArgumentOutOfRangeException(nameof(button));
        return new InputBinding(null, button);
    }

    internal bool IsValid => Key.HasValue ^ MouseButton.HasValue;
}

public interface IInputState
{
    bool Available { get; }
    Vector2 MousePosition { get; }
    Vector2 MouseDelta { get; }
    Vector2 ScrollDelta { get; }
    Vector2D<double> MouseViewportPosition { get; }
    double ViewportAspectRatio { get; }
    string TextInput { get; }

    event Action<InputKey>? KeyPressed;
    event Action<InputKey>? KeyReleased;
    event Action<char>? CharacterTyped;
    event Action<InputMouseButton>? MouseButtonPressed;
    event Action<InputMouseButton>? MouseButtonReleased;
    event Action<Vector2>? MouseMoved;
    event Action<Vector2>? Scrolled;

    bool IsKeyDown(InputKey key);
    bool WasKeyPressed(InputKey key);
    bool WasKeyReleased(InputKey key);
    bool IsMouseButtonDown(InputMouseButton button);
    bool WasMouseButtonPressed(InputMouseButton button);
    bool WasMouseButtonReleased(InputMouseButton button);
    bool IsActionDown(string action);
    bool WasActionPressed(string action);
    bool WasActionReleased(string action);
}

public interface IInputActionMap
{
    IReadOnlyCollection<string> Actions { get; }

    IReadOnlyList<InputBinding> GetActionBindings(string action);
    void SetActionBindings(string action, params InputBinding[] bindings);
    bool RemoveAction(string action);
}

public interface ICursorController
{
    bool IsCursorModeSupported(InputCursorMode mode);
    void SetCursorMode(InputCursorMode mode);
}

public interface IInputService : IInputState, IInputActionMap, ICursorController;
