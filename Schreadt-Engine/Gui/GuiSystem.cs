using Schreadt_Engine.Core;

namespace Schreadt_Engine.Gui;

public sealed class GuiSystem
{
    private readonly List<GuiLabel> _labels = [];

    public IReadOnlyList<GuiLabel> Labels => _labels;

    public GuiLabel AddLabel(string text)
    {
        var label = new GuiLabel(text);
        _labels.Add(label);
        return label;
    }

    public bool RemoveLabel(GuiLabel label)
    {
        ArgumentNullException.ThrowIfNull(label);
        return _labels.Remove(label);
    }

    public void Render(IRenderContext2D renderer)
    {
        ArgumentNullException.ThrowIfNull(renderer);

        foreach (var label in _labels.ToArray())
        {
            if (label.Visible)
            {
                renderer.DrawText(
                    label.Text,
                    label.Position,
                    label.Scale,
                    label.Color,
                    label.BackgroundColor,
                    label.Padding);
            }
        }
    }
}
