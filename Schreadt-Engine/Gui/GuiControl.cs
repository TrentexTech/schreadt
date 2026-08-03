using Silk.NET.Maths;

namespace Schreadt_Engine.Gui;

/// <summary>
/// Base class for GUI elements that can receive pointer interaction.
/// </summary>
public abstract class GuiControl : GuiElement
{
    private bool _enabled = true;

    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (_enabled == value) return;

            _enabled = value;
            if (!value) ResetInteraction();
        }
    }

    public bool IsHovered { get; private set; }

    public bool IsPressed { get; private set; }

    public event EventHandler? PointerEntered;

    public event EventHandler? PointerExited;

    public event EventHandler? Pressed;

    public event EventHandler? Released;

    public event EventHandler? Clicked;

    public bool HitTest(Vector2D<float> point)
    {
        return Visible && Enabled && Bounds.Width > 0.0f && Bounds.Height > 0.0f &&
               Bounds.Contains(point) && OnHitTest(point);
    }

    protected virtual bool OnHitTest(Vector2D<float> point) => true;

    internal void SetHovered(bool hovered)
    {
        hovered &= Visible && Enabled;
        if (IsHovered == hovered) return;

        IsHovered = hovered;
        if (hovered) PointerEntered?.Invoke(this, EventArgs.Empty);
        else PointerExited?.Invoke(this, EventArgs.Empty);
    }

    internal void BeginPress()
    {
        if (!Visible || !Enabled || IsPressed) return;

        IsPressed = true;
        Pressed?.Invoke(this, EventArgs.Empty);
    }

    internal void EndPress(bool activate)
    {
        if (!IsPressed) return;

        IsPressed = false;
        Released?.Invoke(this, EventArgs.Empty);
        if (activate && Visible && Enabled) Clicked?.Invoke(this, EventArgs.Empty);
    }

    internal void CancelPress()
    {
        IsPressed = false;
    }

    internal void ResetInteraction()
    {
        SetHovered(false);
        CancelPress();
    }
}
