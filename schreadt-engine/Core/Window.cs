using Schreadt_Engine.Component;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;

namespace Schreadt_Engine.Core;

public class Window
{
    private Application _app;
    private readonly IWindow _window;
    private Renderer? _renderer;

    internal Window(Application app)
    {
        _app = app;

        var title = Config.Data.Window.Title;
        var size = new Vector2D<int>(Config.Data.Window.DefaultSize.Width, Config.Data.Window.DefaultSize.Height);

        var options = WindowOptions.Default;
        options.Size = size;
        options.Title = title;

        _window = Silk.NET.Windowing.Window.Create(options);

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

    private void OnLoad()
    {
        var gl = GL.GetApi(_window);
        _renderer = new Renderer(gl);
        OnFramebufferResize(_window.FramebufferSize);
    }

    private void OnUpdate(double dt)
    {
        _app.Update(dt);
    }

    private void OnRender(double dt)
    {
        if (_renderer is not null) _app.Render(_renderer);
    }

    private void OnFramebufferResize(Vector2D<int> size)
    {
        _renderer?.Resize(size.X, size.Y);
    }

    private void OnClosing()
    {
        _renderer?.Dispose();
        _renderer = null;
    }
}
