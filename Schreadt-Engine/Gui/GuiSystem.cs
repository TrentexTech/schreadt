using Schreadt_Engine.Core;
using Silk.NET.Maths;

namespace Schreadt_Engine.Gui;

public sealed class GuiSystem
{
    private readonly List<IGuiElement> _elements = [];
    private GuiControl? _capturedControl;
    private GuiControl? _hoveredControl;

    public IReadOnlyList<IGuiElement> Elements => _elements;

    public IReadOnlyList<GuiLabel> Labels => _elements.OfType<GuiLabel>().ToArray();

    public bool IsPointerOverControl => _hoveredControl is not null;

    public bool IsPointerCaptured => _capturedControl is not null;

    public T Add<T>(T element) where T : IGuiElement
    {
        ArgumentNullException.ThrowIfNull(element);
        if (_elements.Contains(element)) throw new InvalidOperationException("The GUI element is already registered.");
        _elements.Add(element);
        return element;
    }

    public GuiLabel AddLabel(string text)
    {
        return Add(new GuiLabel(text));
    }

    public GuiPanel AddPanel() => Add(new GuiPanel());

    public GuiButton AddButton(string text) => Add(new GuiButton(text));

    public bool RemoveLabel(GuiLabel label)
    {
        return Remove(label);
    }

    public bool Remove(IGuiElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (!_elements.Remove(element)) return false;

        if (_hoveredControl is not null && Contains(element, _hoveredControl))
        {
            _hoveredControl.SetHovered(false);
            _hoveredControl = null;
        }

        if (_capturedControl is not null && Contains(element, _capturedControl))
        {
            _capturedControl.CancelPress();
            _capturedControl = null;
        }

        return true;
    }

    public void Update(IInputState input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var pointerPosition = new Vector2D<float>(input.MousePosition.X, input.MousePosition.Y);
        var hoveredControl = FindTopmostControl(pointerPosition);
        SetHoveredControl(hoveredControl);

        if (_capturedControl is not null &&
            (!IsInteractable(_capturedControl) || !_capturedControl.Enabled))
        {
            _capturedControl.CancelPress();
            _capturedControl = null;
        }

        if (input.WasMouseButtonPressed(InputMouseButton.Left))
        {
            _capturedControl?.CancelPress();
            _capturedControl = hoveredControl;
            _capturedControl?.BeginPress();
        }

        if (input.WasMouseButtonReleased(InputMouseButton.Left) && _capturedControl is not null)
        {
            var capturedControl = _capturedControl;
            _capturedControl = null;
            capturedControl.EndPress(ReferenceEquals(capturedControl, hoveredControl));
        }
    }

    public void Render(IRenderContext2D renderer)
    {
        ArgumentNullException.ThrowIfNull(renderer);

        var viewportSize = new Vector2D<float>(renderer.ViewportSize.X, renderer.ViewportSize.Y);
        foreach (var element in _elements.ToArray())
        {
            if (!element.Visible) continue;

            var availableSize = new Vector2D<float>(
                Math.Max(0.0f, viewportSize.X - element.Position.X),
                Math.Max(0.0f, viewportSize.Y - element.Position.Y));
            element.Measure(availableSize);
            element.Arrange(new GuiRectangle(element.Position, element.DesiredSize));
            element.Render(renderer);
        }
    }

    private GuiControl? FindTopmostControl(Vector2D<float> pointerPosition)
    {
        for (var index = _elements.Count - 1; index >= 0; index--)
        {
            var match = FindTopmostControl(_elements[index], pointerPosition);
            if (match is not null) return match;
        }

        return null;
    }

    private static GuiControl? FindTopmostControl(IGuiElement element, Vector2D<float> pointerPosition)
    {
        if (!element.Visible) return null;

        if (element is GuiPanel panel)
        {
            for (var index = panel.Children.Count - 1; index >= 0; index--)
            {
                var match = FindTopmostControl(panel.Children[index], pointerPosition);
                if (match is not null) return match;
            }
        }

        return element is GuiControl control && control.HitTest(pointerPosition) ? control : null;
    }

    private void SetHoveredControl(GuiControl? control)
    {
        if (ReferenceEquals(_hoveredControl, control)) return;

        _hoveredControl?.SetHovered(false);
        _hoveredControl = control;
        _hoveredControl?.SetHovered(true);
    }

    private bool IsInteractable(GuiControl control)
    {
        return _elements.Any(element => IsVisibleDescendant(element, control));
    }

    private static bool Contains(IGuiElement root, IGuiElement candidate)
    {
        return ReferenceEquals(root, candidate) ||
               root is GuiPanel panel && panel.Children.Any(child => Contains(child, candidate));
    }

    private static bool IsVisibleDescendant(IGuiElement root, IGuiElement candidate)
    {
        if (!root.Visible) return false;
        if (ReferenceEquals(root, candidate)) return true;

        return root is GuiPanel panel &&
               panel.Children.Any(child => IsVisibleDescendant(child, candidate));
    }
}
