using System.Numerics;
using System.Text;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.Windowing;

namespace Schreadt_Engine.Core;

public sealed class InputManager : IDisposable
{
    private readonly HashSet<Key> _keysDown = [];
    private readonly HashSet<Key> _keysPressed = [];
    private readonly HashSet<Key> _keysReleased = [];
    private readonly HashSet<MouseButton> _mouseButtonsDown = [];
    private readonly HashSet<MouseButton> _mouseButtonsPressed = [];
    private readonly HashSet<MouseButton> _mouseButtonsReleased = [];
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

    public event Action<Key>? KeyPressed;
    public event Action<Key>? KeyReleased;
    public event Action<char>? CharacterTyped;
    public event Action<MouseButton>? MouseButtonPressed;
    public event Action<MouseButton>? MouseButtonReleased;
    public event Action<Vector2>? MouseMoved;
    public event Action<Vector2>? Scrolled;

    public bool IsKeyDown(Key key) => _keysDown.Contains(key);

    public bool WasKeyPressed(Key key) => _keysPressed.Contains(key);

    public bool WasKeyReleased(Key key) => _keysReleased.Contains(key);

    public bool IsMouseButtonDown(MouseButton button) => _mouseButtonsDown.Contains(button);

    public bool WasMouseButtonPressed(MouseButton button) => _mouseButtonsPressed.Contains(button);

    public bool WasMouseButtonReleased(MouseButton button) => _mouseButtonsReleased.Contains(button);

    public bool IsCursorModeSupported(CursorMode mode) => _mouse?.Cursor.IsSupported(mode) == true;

    public void SetCursorMode(CursorMode mode)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var cursor = _mouse?.Cursor ?? throw new InvalidOperationException("No mouse is connected.");
        if (!cursor.IsSupported(mode)) throw new NotSupportedException($"Cursor mode '{mode}' is not supported by SDL.");
        cursor.CursorMode = mode;
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
        _disposed = true;
    }

    private void OnKeyDown(IKeyboard keyboard, Key key, int scanCode)
    {
        if (!_keysDown.Add(key)) return;

        _keysPressed.Add(key);
        KeyPressed?.Invoke(key);
    }

    private void OnKeyUp(IKeyboard keyboard, Key key, int scanCode)
    {
        _keysDown.Remove(key);
        _keysReleased.Add(key);
        KeyReleased?.Invoke(key);
    }

    private void OnKeyChar(IKeyboard keyboard, char character)
    {
        _textInput.Append(character);
        CharacterTyped?.Invoke(character);
    }

    private void OnMouseDown(IMouse mouse, MouseButton button)
    {
        if (!_mouseButtonsDown.Add(button)) return;

        _mouseButtonsPressed.Add(button);
        MouseButtonPressed?.Invoke(button);
    }

    private void OnMouseUp(IMouse mouse, MouseButton button)
    {
        _mouseButtonsDown.Remove(button);
        _mouseButtonsReleased.Add(button);
        MouseButtonReleased?.Invoke(button);
    }

    private void OnMouseMove(IMouse mouse, Vector2 position)
    {
        var delta = position - MousePosition;
        MousePosition = position;
        MouseDelta += delta;
        MouseMoved?.Invoke(position);
    }

    private void OnScroll(IMouse mouse, ScrollWheel wheel)
    {
        var delta = new Vector2(wheel.X, wheel.Y);
        ScrollDelta += delta;
        Scrolled?.Invoke(delta);
    }
}
