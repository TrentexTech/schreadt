using Silk.NET.Maths;

namespace Schreadt_Engine.Core;

public enum WindowDisplayState
{
    Normal,
    Minimized,
    Maximized,
    /// <summary>Exclusive fullscreen, which may change the monitor's display mode.</summary>
    Fullscreen,
    /// <summary>Borderless desktop fullscreen using the monitor's current display mode.</summary>
    BorderlessFullscreen
}

/// <summary>
/// Backend-independent controls for the active game window.
/// </summary>
public interface IWindowController
{
    string Title { get; set; }

    Vector2D<int> Size { get; set; }

    Vector2D<int> FramebufferSize { get; }

    WindowDisplayState DisplayState { get; set; }

    bool VSync { get; set; }

    bool IsCloseRequested { get; }

    void ToggleFullscreen();

    void ToggleBorderlessFullscreen();

    void RequestClose();
}
