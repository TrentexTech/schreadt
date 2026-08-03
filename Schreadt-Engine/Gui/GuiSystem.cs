using Schreadt_Engine.Core;
using Silk.NET.Maths;

namespace Schreadt_Engine.Gui;

public sealed class GuiSystem
{
    private readonly List<IGuiElement> _elements = [];
    private readonly List<GuiLayer> _layers = [];
    private GuiControl? _capturedControl;
    private GuiControl? _hoveredControl;

    /// <summary>Persistent GUI roots that are not owned by a scene.</summary>
    public IReadOnlyList<IGuiElement> Elements => _elements;

    public IReadOnlyList<GuiLayer> Layers => _layers;

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

    public GuiLabel AddLabel(string text) => Add(new GuiLabel(text));

    public GuiPanel AddPanel() => Add(new GuiPanel());

    public GuiButton AddButton(string text) => Add(new GuiButton(text));

    public GuiLayer AddLayer(GuiLayer layer)
    {
        ArgumentNullException.ThrowIfNull(layer);
        if (_layers.Contains(layer)) throw new InvalidOperationException("The GUI layer is already registered.");

        layer.Attach(this);
        _layers.Add(layer);
        return layer;
    }

    public bool RemoveLayer(GuiLayer layer)
    {
        ArgumentNullException.ThrowIfNull(layer);
        if (!_layers.Remove(layer)) return false;
        layer.Detach(this);
        return true;
    }

    public bool RemoveLabel(GuiLabel label) => Remove(label);

    public bool Remove(IGuiElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (!_elements.Remove(element)) return false;
        ReleaseInteraction(element);
        return true;
    }

    public void Update(IInputState input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (input.WasKeyPressed(InputKey.Escape)) DismissTopScreenOnEscape();

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

        foreach (var layer in _layers.ToArray())
        {
            if (!layer.Visible || !_layers.Contains(layer)) continue;
            foreach (var root in layer.EnumerateRoots().ToArray()) RenderRoot(root, viewportSize, renderer);
        }

        foreach (var element in _elements.ToArray()) RenderRoot(element, viewportSize, renderer);
    }

    internal void ReleaseInteraction(IGuiElement root)
    {
        if (_hoveredControl is not null && Contains(root, _hoveredControl))
        {
            _hoveredControl.SetHovered(false);
            _hoveredControl = null;
        }

        if (_capturedControl is not null && Contains(root, _capturedControl))
        {
            _capturedControl.CancelPress();
            _capturedControl = null;
        }
    }

    private static void RenderRoot(
        IGuiElement element,
        Vector2D<float> viewportSize,
        IRenderContext2D renderer)
    {
        if (!element.Visible) return;

        var availableSize = new Vector2D<float>(
            Math.Max(0.0f, viewportSize.X - element.Position.X),
            Math.Max(0.0f, viewportSize.Y - element.Position.Y));
        element.Measure(availableSize);
        element.Arrange(new GuiRectangle(element.Position, element.DesiredSize));
        element.Render(renderer);
    }

    private GuiControl? FindTopmostControl(Vector2D<float> pointerPosition)
    {
        for (var index = _elements.Count - 1; index >= 0; index--)
        {
            var match = FindTopmostControl(_elements[index], pointerPosition);
            if (match is not null) return match;
        }

        for (var layerIndex = _layers.Count - 1; layerIndex >= 0; layerIndex--)
        {
            var layer = _layers[layerIndex];
            if (!layer.Visible || !layer.InputEnabled) continue;

            var screenResult = FindScreenControl(layer, pointerPosition);
            if (screenResult.Control is not null) return screenResult.Control;
            if (screenResult.BlocksInputBelow) return null;

            for (var index = layer.Elements.Count - 1; index >= 0; index--)
            {
                var match = FindTopmostControl(layer.Elements[index], pointerPosition);
                if (match is not null) return match;
            }
        }

        return null;
    }

    private static ScreenSearchResult FindScreenControl(GuiLayer layer, Vector2D<float> pointerPosition)
    {
        for (var index = layer.Screens.Screens.Count - 1; index >= 0; index--)
        {
            var screen = layer.Screens.Screens[index];
            var match = FindTopmostControl(screen.Root, pointerPosition);
            if (match is not null) return new ScreenSearchResult(match, true);
            if (screen.IsModal && screen.Root.Visible) return new ScreenSearchResult(null, true);
        }

        return default;
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
        if (_elements.Any(element => IsVisibleDescendant(element, control))) return true;

        foreach (var layer in _layers)
        {
            if (!layer.Visible || !layer.InputEnabled) continue;

            var blocked = false;
            for (var index = layer.Screens.Screens.Count - 1; index >= 0; index--)
            {
                var screen = layer.Screens.Screens[index];
                if (IsVisibleDescendant(screen.Root, control)) return !blocked;
                if (screen.IsModal && screen.Root.Visible) blocked = true;
            }

            if (!blocked && layer.Elements.Any(element => IsVisibleDescendant(element, control))) return true;
        }

        return false;
    }

    private void DismissTopScreenOnEscape()
    {
        for (var index = _layers.Count - 1; index >= 0; index--)
        {
            var layer = _layers[index];
            if (!layer.Visible || !layer.InputEnabled) continue;
            var screen = layer.Screens.Top;
            if (screen is null) continue;
            if (screen.DismissOnEscape) layer.Screens.Pop();
            return;
        }
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

    private readonly record struct ScreenSearchResult(GuiControl? Control, bool BlocksInputBelow);
}
