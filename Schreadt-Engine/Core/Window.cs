using System.Diagnostics;
using Schreadt_Engine.Component;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.SDL;
using SdlApi = Silk.NET.SDL.Sdl;
using SdlWindow = Silk.NET.SDL.Window;

namespace Schreadt_Engine.Core;

public sealed unsafe class Window : IWindowController
{
    private readonly Application _app;
    private IRenderer2D? _renderer;
    private SdlApi? _sdl;
    private SdlWindow* _window;
    private void* _glContext;
    private WindowDisplayState _windowedState = WindowDisplayState.Normal;
    private string _title;
    private Vector2D<int> _size;
    private WindowDisplayState _displayState = WindowDisplayState.Normal;
    private bool _vsync = true;
    private bool _sdlInitialized;
    private bool _loaded;
    private bool _closeRequested;
    private bool _closing;

    public string Title
    {
        get => _title;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _title = value;
            if (_loaded) _sdl!.SetWindowTitle(_window, value);
        }
    }

    public Vector2D<int> Size
    {
        get
        {
            if (!_loaded) return _size;
            var width = 0;
            var height = 0;
            _sdl!.GetWindowSize(_window, ref width, ref height);
            return _size = new Vector2D<int>(width, height);
        }
        set
        {
            if (value.X <= 0 || value.Y <= 0)
                throw new ArgumentOutOfRangeException(nameof(value), "Window dimensions must be greater than zero.");

            _size = value;
            if (_loaded) _sdl!.SetWindowSize(_window, value.X, value.Y);
        }
    }

    public Vector2D<int> FramebufferSize
    {
        get
        {
            if (!_loaded) return _size;
            var width = 0;
            var height = 0;
            _sdl!.GLGetDrawableSize(_window, ref width, ref height);
            return new Vector2D<int>(width, height);
        }
    }

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

            if (_loaded) ApplyDisplayState(currentState, value);
            _displayState = value;
            if (currentState != value)
                EngineLog.Information($"Window display state changed from {currentState} to {value}.", "Window");
        }
    }

    public bool VSync
    {
        get => _vsync;
        set
        {
            if (_loaded) SetSwapInterval(value);
            if (_vsync != value) EngineLog.Information($"VSync {(value ? "enabled" : "disabled")}.", "Window");
            _vsync = value;
        }
    }

    public bool IsCloseRequested => _closeRequested;

    internal Window(Application app)
    {
        _app = app;
        _title = Config.Data.Window.Title;
        _size = new Vector2D<int>(Config.Data.Window.DefaultSize.Width, Config.Data.Window.DefaultSize.Height);
        EngineLog.Debug($"Window configured as '{_title}' at {_size.X}x{_size.Y}.", "Window");
    }

    internal void Run()
    {
        try
        {
            Load();
            RunMainLoop();
        }
        finally
        {
            Close();
        }
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
        if (!_closeRequested) EngineLog.Debug("Window close requested.", "Window");
        _closeRequested = true;
    }

    private void Load()
    {
        EngineLog.Information(
            $"Loading SDL video and OpenGL 3.3 core window '{_title}' at {_size.X}x{_size.Y}.",
            "Window");
        _sdl = SdlApi.GetApi();
        ThrowIfSdlError(_sdl.Init(SdlApi.InitVideo | SdlApi.InitEvents), "initialize SDL");
        _sdlInitialized = true;

        SetGlAttribute(GLattr.ContextMajorVersion, 3);
        SetGlAttribute(GLattr.ContextMinorVersion, 3);
        SetGlAttribute(GLattr.ContextProfileMask, (int)GLprofile.Core);
        SetGlAttribute(GLattr.ContextFlags, (int)GLcontextFlag.ForwardCompatibleFlag);
        SetGlAttribute(GLattr.Doublebuffer, 1);

        const WindowFlags flags = WindowFlags.Opengl |
                                  WindowFlags.Shown |
                                  WindowFlags.Resizable |
                                  WindowFlags.AllowHighdpi;
        _window = _sdl.CreateWindow(
            _title,
            SdlApi.WindowposCentered,
            SdlApi.WindowposCentered,
            _size.X,
            _size.Y,
            (uint)flags);
        if (_window == null) ThrowSdlError("create the window");

        _glContext = _sdl.GLCreateContext(_window);
        if (_glContext == null) ThrowSdlError("create the OpenGL context");
        ThrowIfSdlError(_sdl.GLMakeCurrent(_window, _glContext), "make the OpenGL context current");
        SetSwapInterval(_vsync);

        var gl = GL.GetApi(name => (nint)_sdl.GLGetProcAddress(name));
        var defaultSize = Config.Data.Window.DefaultSize;
        _renderer = new Renderer(gl, State.Assets, (double)defaultSize.Width / defaultSize.Height);
        _loaded = true;
        if (_displayState != WindowDisplayState.Normal)
            ApplyDisplayState(WindowDisplayState.Normal, _displayState);

        _app.Input.Initialize(_sdl, this);
        _sdl.StartTextInput();
        ResizeRenderer();
        var framebuffer = FramebufferSize;
        EngineLog.Information(
            $"Window loaded. Window: {Size.X}x{Size.Y}; framebuffer: {framebuffer.X}x{framebuffer.Y}; " +
            $"viewport: {_renderer.ViewportSize.X}x{_renderer.ViewportSize.Y} at " +
            $"({_renderer.ViewportOffset.X}, {_renderer.ViewportOffset.Y}); VSync: {_vsync}.",
            "Window");
    }

    private void RunMainLoop()
    {
        EngineLog.Information("Main loop started.", "Window");
        var previousTimestamp = Stopwatch.GetTimestamp();

        while (!_closeRequested)
        {
            PollEvents();
            if (_closeRequested) break;

            var timestamp = Stopwatch.GetTimestamp();
            var frameTime = (timestamp - previousTimestamp) / (double)Stopwatch.Frequency;
            previousTimestamp = timestamp;

            _app.Update(frameTime);
            if (_closeRequested) break;

            _app.Render(_renderer!, frameTime);
            _sdl!.GLSwapWindow(_window);
        }

        EngineLog.Information($"Main loop exited after {_app.Runtime.FrameCount} frame(s).", "Window");
    }

    private void PollEvents()
    {
        Event currentEvent = default;
        while (_sdl!.PollEvent(ref currentEvent) != 0)
        {
            switch ((EventType)currentEvent.Type)
            {
                case EventType.Quit:
                    RequestClose();
                    break;
                case EventType.Windowevent:
                    ProcessWindowEvent(currentEvent.Window);
                    break;
                case EventType.Keydown:
                    _app.Input.ProcessKeyDown(InputManager.TranslateScancode(currentEvent.Key.Keysym.Scancode));
                    break;
                case EventType.Keyup:
                    _app.Input.ProcessKeyUp(InputManager.TranslateScancode(currentEvent.Key.Keysym.Scancode));
                    break;
                case EventType.Textinput:
                    ProcessTextInput(currentEvent.Text);
                    break;
                case EventType.Mousemotion:
                    _app.Input.ProcessMouseMove(
                        new System.Numerics.Vector2(currentEvent.Motion.X, currentEvent.Motion.Y),
                        new System.Numerics.Vector2(currentEvent.Motion.Xrel, currentEvent.Motion.Yrel));
                    break;
                case EventType.Mousebuttondown:
                    _app.Input.ProcessMouseDown(InputManager.TranslateMouseButton(currentEvent.Button.Button));
                    break;
                case EventType.Mousebuttonup:
                    _app.Input.ProcessMouseUp(InputManager.TranslateMouseButton(currentEvent.Button.Button));
                    break;
                case EventType.Mousewheel:
                    var direction = (MouseWheelDirection)currentEvent.Wheel.Direction;
                    var multiplier = direction == MouseWheelDirection.Flipped ? -1.0f : 1.0f;
                    _app.Input.ProcessScroll(new System.Numerics.Vector2(
                        currentEvent.Wheel.PreciseX * multiplier,
                        currentEvent.Wheel.PreciseY * multiplier));
                    break;
            }
        }
    }

    private void ProcessWindowEvent(WindowEvent windowEvent)
    {
        switch ((WindowEventID)windowEvent.Event)
        {
            case WindowEventID.Close:
                RequestClose();
                break;
            case WindowEventID.Resized:
            case WindowEventID.SizeChanged:
                _size = new Vector2D<int>(windowEvent.Data1, windowEvent.Data2);
                ResizeRenderer();
                break;
            case WindowEventID.Minimized:
                if (!IsFullscreen(_displayState)) _displayState = WindowDisplayState.Minimized;
                EngineLog.Debug("Window minimized.", "Window");
                break;
            case WindowEventID.Maximized:
                if (!IsFullscreen(_displayState))
                {
                    _displayState = WindowDisplayState.Maximized;
                    _windowedState = WindowDisplayState.Maximized;
                }
                EngineLog.Debug("Window maximized.", "Window");
                break;
            case WindowEventID.Restored:
                if (!IsFullscreen(_displayState))
                {
                    _displayState = WindowDisplayState.Normal;
                    _windowedState = WindowDisplayState.Normal;
                }
                EngineLog.Debug("Window restored.", "Window");
                break;
            case WindowEventID.FocusLost:
                EngineLog.Debug("Window focus lost.", "Window");
                _app.Input.ProcessFocusLost();
                break;
        }
    }

    private void ProcessTextInput(TextInputEvent textInputEvent)
    {
        byte* text = textInputEvent.Text;
        var input = System.Runtime.InteropServices.Marshal.PtrToStringUTF8((nint)text);
        if (input is null) return;
        foreach (var character in input) _app.Input.ProcessCharacterTyped(character);
    }

    private void ResizeRenderer()
    {
        var framebufferSize = FramebufferSize;
        var windowSize = Size;
        EngineLog.Debug($"Framebuffer resized to {framebufferSize.X}x{framebufferSize.Y}.", "Window");
        _renderer?.Resize(framebufferSize.X, framebufferSize.Y);
        if (_renderer is null) return;

        _app.Input.SetViewportSizes(framebufferSize, windowSize, _renderer.ViewportOffset, _renderer.ViewportSize);
        _app.Gui.SetViewportSizes(framebufferSize, windowSize, _renderer.ViewportOffset, _renderer.ViewportSize);
    }

    private void Close()
    {
        if (_closing) return;
        _closing = true;
        _closeRequested = true;
        EngineLog.Information("Window is closing.", "Window");

        try
        {
            _app.Shutdown();
        }
        finally
        {
            if (_sdl is not null && _sdlInitialized) _sdl.StopTextInput();
            _app.Input.Dispose();
            _renderer?.Dispose();
            _renderer = null;

            if (_sdl is not null && _glContext != null)
            {
                _sdl.GLDeleteContext(_glContext);
                _glContext = null;
            }

            if (_sdl is not null && _window != null)
            {
                _sdl.DestroyWindow(_window);
                _window = null;
            }

            if (_sdl is not null && _sdlInitialized)
            {
                _sdl.Quit();
                _sdlInitialized = false;
            }

            _sdl?.Dispose();
            _sdl = null;
            _loaded = false;
            EngineLog.Information("Window and SDL resources closed.", "Window");
        }
    }

    private void ApplyDisplayState(
        WindowDisplayState currentState,
        WindowDisplayState requestedState)
    {
        if (requestedState == WindowDisplayState.Fullscreen)
        {
            SetSdlFullscreen((uint)WindowFlags.Fullscreen, "enter exclusive fullscreen mode");
            return;
        }

        if (requestedState == WindowDisplayState.BorderlessFullscreen)
        {
            SetSdlFullscreen((uint)WindowFlags.FullscreenDesktop, "enter borderless fullscreen mode");
            return;
        }

        if (IsFullscreen(currentState)) SetSdlFullscreen(0u, "leave fullscreen mode");

        switch (requestedState)
        {
            case WindowDisplayState.Normal:
                _sdl!.RestoreWindow(_window);
                break;
            case WindowDisplayState.Minimized:
                _sdl!.MinimizeWindow(_window);
                break;
            case WindowDisplayState.Maximized:
                _sdl!.MaximizeWindow(_window);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(requestedState), requestedState, "Unknown window state.");
        }
    }

    private void SetSdlFullscreen(uint flags, string operation)
    {
        ThrowIfSdlError(_sdl!.SetWindowFullscreen(_window, flags), operation);
    }

    private void SetSwapInterval(bool enabled)
    {
        ThrowIfSdlError(_sdl!.GLSetSwapInterval(enabled ? 1 : 0), $"{(enabled ? "enable" : "disable")} VSync");
        EngineLog.Debug($"SDL swap interval set to {(enabled ? 1 : 0)}.", "Window");
    }

    private void SetGlAttribute(GLattr attribute, int value)
    {
        ThrowIfSdlError(_sdl!.GLSetAttribute(attribute, value), $"set OpenGL attribute {attribute}");
    }

    private void ThrowIfSdlError(int result, string operation)
    {
        if (result != 0) ThrowSdlError(operation);
    }

    private void ThrowSdlError(string operation)
    {
        throw new InvalidOperationException($"SDL could not {operation}: {_sdl?.GetErrorS() ?? "unknown error"}");
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
