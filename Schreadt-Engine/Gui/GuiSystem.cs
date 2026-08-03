using Schreadt_Engine.Core;
using Silk.NET.Maths;

namespace Schreadt_Engine.Gui;

public sealed class GuiSystem
{
    private readonly List<IGuiElement> _elements = [];

    public IReadOnlyList<IGuiElement> Elements => _elements;

    public IReadOnlyList<GuiLabel> Labels => _elements.OfType<GuiLabel>().ToArray();

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

    public bool RemoveLabel(GuiLabel label)
    {
        return Remove(label);
    }

    public bool Remove(IGuiElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return _elements.Remove(element);
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
}
