using Schreadt_Engine.Component;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using Silk.NET.Windowing.Sdl;
using SdlApi = Silk.NET.SDL.Sdl;

namespace Schreadt_Engine.Core;

public sealed class Window : IWindowController
{
    private readonly Application _app;
    private readonly IWindow _window;
    private IRenderer2D? _renderer;
    private SdlApi? _sdl;
    private WindowDisplayState _windowedState = WindowDisplayState.Normal;
    private string _title;
    private Vector2D<int> _size;
    private WindowDisplayState _displayState = WindowDisplayState.Normal;
    private bool _vsync;
    private bool _loaded;
    private bool _closeRequested;
    private bool _closeSubmitted;
    private bool _closing;

    public string Title
    {
        get => _loaded ? _window.Title : _title;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _title = value;
            if (_loaded) _window.Title = value;
        }
    }

    public Vector2D<int> Size
    {
        get => _loaded ? _window.Size : _size;
        set
        {
            if (value.X <= 0 || value.Y <= 0)
                throw new ArgumentOutOfRangeException(nameof(value), "Window dimensions must be greater than zero.");

            _size = value;
            if (_loaded) _window.Size = value;
        }
    }

    public Vector2D<int> FramebufferSize => _loaded ? _window.FramebufferSize : _size;

    public WindowDisplayState DisplayState
    {
        get => _displayState;
        set
        {
            if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));

            var currentState = _displayState;
            if (IsFullscreen(value) && !IsFullscreen(currentState))
            {
                _windowedState = currentState == WindowDisplayState.Maximized
                    ? WindowDisplayState.Maximized
                    : WindowDisplayState.Normal;
            }

            if (_loaded) ApplyBackendDisplayState(currentState, value);
            _displayState = value;
        }
    }

    public bool VSync
    {
        get => _loaded ? _window.VSync : _vsync;
        set
        {
            _vsync = value;
            if (_loaded) _window.VSync = value;
        }
    }

    public bool IsCloseRequested => _closeRequested;

    internal Window(Application app)
    {
        _app = app;

        SdlWindowing.Use();

        var title = Config.Data.Window.Title;
        var size = new Vector2D<int>(Config.Data.Window.DefaultSize.Width, Config.Data.Window.DefaultSize.Height);

        var options = WindowOptions.Default;
        options.Size = size;
        options.Title = title;

        _title = options.Title;
        _size = options.Size;
        _displayState = FromBackendState(options.WindowState);
        _vsync = options.VSync;

        _window = Silk.NET.Windowing.Window.Create(options);

        if (!SdlWindowing.IsViewSdl(_window))
            throw new InvalidOperationException("Silk.NET did not create the window with the SDL backend.");

        _window.Load += OnLoad;
        _window.Update += OnUpdate;
        _window.Render += OnRender;
        _window.FramebufferResize += OnFramebufferResize;
        _window.Closing += OnClosing;
    }

    internal void Run()
    {
        _window.Run();
    }

    public void ToggleFullscreen()
    {
        DisplayState = GetFullscreenToggleTarget(
            _displayState,
            _windowedState,
            WindowDisplayState.Fullscreen);
    }

    public void ToggleBorderlessFullscreen()
    {
        DisplayState = GetFullscreenToggleTarget(
            _displayState,
            _windowedState,
            WindowDisplayState.BorderlessFullscreen);
    }

    public void RequestClose()
    {
        _closeRequested = true;
    }

    private void OnLoad()
    {
        _loaded = true;
        _sdl = SdlApi.GetApi();
        _window.Title = _title;
        _window.Size = _size;
        ApplyBackendDisplayState(WindowDisplayState.Normal, _displayState);
        _window.VSync = _vsync;
        _app.Input.Initialize(_window);
        var gl = GL.GetApi(_window);
        _renderer = new Renderer(gl, State.Assets);
        OnFramebufferResize(_window.FramebufferSize);
        SubmitCloseIfRequested();
    }

    private void OnUpdate(double dt)
    {
        if (!_closeRequested) _app.Update(dt);
        SubmitCloseIfRequested();
    }

    private void OnRender(double dt)
    {
        if (!_closeRequested && _renderer is not null) _app.Render(_renderer, dt);
    }

    private void OnFramebufferResize(Vector2D<int> size)
    {
        _renderer?.Resize(size.X, size.Y);
    }

    private void OnClosing()
    {
        _closeRequested = true;
        if (_closing) return;
        _closing = true;

        try
        {
            _app.Shutdown();
        }
        finally
        {
            _app.Input.Dispose();
            _renderer?.Dispose();
            _renderer = null;
            _sdl?.Dispose();
            _sdl = null;
            _loaded = false;
        }
    }

    private void SubmitCloseIfRequested()
    {
        if (!_closeRequested || _closeSubmitted || _closing) return;

        _closeSubmitted = true;
        _window.Close();
    }

    private unsafe void ApplyBackendDisplayState(
        WindowDisplayState currentState,
        WindowDisplayState requestedState)
    {
        var sdl = _sdl
            ?? throw new InvalidOperationException("The SDL API is not available for the active window.");
        var sdlWindow = (Silk.NET.SDL.Window*)_window.Handle;

        if (requestedState == WindowDisplayState.Fullscreen)
        {
            SetSdlFullscreen(
                sdl,
                sdlWindow,
                (uint)Silk.NET.SDL.WindowFlags.Fullscreen,
                "enter exclusive fullscreen mode");
            return;
        }

        if (requestedState == WindowDisplayState.BorderlessFullscreen)
        {
            SetSdlFullscreen(
                sdl,
                sdlWindow,
                (uint)Silk.NET.SDL.WindowFlags.FullscreenDesktop,
                "enter borderless fullscreen mode");
            return;
        }

        if (IsFullscreen(currentState))
            SetSdlFullscreen(sdl, sdlWindow, 0u, "leave fullscreen mode");

        _window.WindowState = ToBackendState(requestedState);
    }

    private static unsafe void SetSdlFullscreen(
        SdlApi sdl,
        Silk.NET.SDL.Window* window,
        uint flags,
        string operation)
    {
        var result = sdl.SetWindowFullscreen(window, flags);
        if (result != 0)
            throw new InvalidOperationException($"SDL could not {operation}: {sdl.GetErrorS()}");
    }

    private static WindowDisplayState FromBackendState(WindowState state)
    {
        return state switch
        {
            WindowState.Normal => WindowDisplayState.Normal,
            WindowState.Minimized => WindowDisplayState.Minimized,
            WindowState.Maximized => WindowDisplayState.Maximized,
            WindowState.Fullscreen => WindowDisplayState.Fullscreen,
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown backend window state.")
        };
    }

    private static WindowState ToBackendState(WindowDisplayState state)
    {
        return state switch
        {
            WindowDisplayState.Normal => WindowState.Normal,
            WindowDisplayState.Minimized => WindowState.Minimized,
            WindowDisplayState.Maximized => WindowState.Maximized,
            WindowDisplayState.Fullscreen => WindowState.Fullscreen,
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown window state.")
        };
    }

    internal static WindowDisplayState GetFullscreenToggleTarget(
        WindowDisplayState currentState,
        WindowDisplayState windowedState,
        WindowDisplayState fullscreenState)
    {
        if (!IsFullscreen(fullscreenState))
            throw new ArgumentOutOfRangeException(nameof(fullscreenState), "The toggle target must be a fullscreen state.");

        return currentState == fullscreenState
            ? windowedState
            : fullscreenState;
    }

    private static bool IsFullscreen(WindowDisplayState currentState)
    {
        return currentState is WindowDisplayState.Fullscreen or WindowDisplayState.BorderlessFullscreen;
    }
}
