using Schreadt_Engine.Core;
using Silk.NET.Maths;

namespace Schreadt_Engine.Gui;

public sealed class GuiPanel : GuiElement
{
    private readonly List<IGuiElement> _children = [];
    private float _padding = 6.0f;
    private float _spacing = 4.0f;

    public IReadOnlyList<IGuiElement> Children => _children;

    public float Padding
    {
        get => _padding;
        set
        {
            if (!float.IsFinite(value) || value < 0.0f)
                throw new ArgumentOutOfRangeException(nameof(value), "Panel padding must be finite and non-negative.");
            _padding = value;
        }
    }

    public float Spacing
    {
        get => _spacing;
        set
        {
            if (!float.IsFinite(value) || value < 0.0f)
                throw new ArgumentOutOfRangeException(nameof(value), "Panel spacing must be finite and non-negative.");
            _spacing = value;
        }
    }

    public Vector4D<float> BackgroundColor { get; set; } = new(0.025f, 0.035f, 0.06f, 0.82f);

    public T Add<T>(T element) where T : IGuiElement
    {
        ArgumentNullException.ThrowIfNull(element);
        if (ReferenceEquals(element, this)) throw new InvalidOperationException("A panel cannot contain itself.");
        if (_children.Contains(element)) throw new InvalidOperationException("The GUI element is already in this panel.");
        if (element is GuiPanel panel && panel.ContainsDescendant(this))
            throw new InvalidOperationException("Adding the panel would create a GUI hierarchy cycle.");

        _children.Add(element);
        return element;
    }

    public GuiLabel AddLabel(string text) => Add(new GuiLabel(text));

    public GuiButton AddButton(string text) => Add(new GuiButton(text));

    public bool Remove(IGuiElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return _children.Remove(element);
    }

    protected override Vector2D<float> OnMeasure(Vector2D<float> availableSize)
    {
        var innerAvailable = new Vector2D<float>(
            Math.Max(0.0f, availableSize.X - Padding * 2.0f),
            Math.Max(0.0f, availableSize.Y - Padding * 2.0f));
        var visibleChildren = _children.Where(child => child.Visible).ToArray();
        var desiredWidth = 0.0f;
        var desiredHeight = 0.0f;

        foreach (var child in visibleChildren)
        {
            child.Measure(innerAvailable);
            desiredWidth = Math.Max(desiredWidth, child.DesiredSize.X);
            desiredHeight += child.DesiredSize.Y;
        }

        if (visibleChildren.Length > 1) desiredHeight += Spacing * (visibleChildren.Length - 1);
        return new Vector2D<float>(desiredWidth + Padding * 2.0f, desiredHeight + Padding * 2.0f);
    }

    protected override void OnArrange(GuiRectangle bounds)
    {
        var childX = bounds.X + Padding;
        var childY = bounds.Y + Padding;
        var innerWidth = Math.Max(0.0f, bounds.Width - Padding * 2.0f);

        foreach (var child in _children.Where(child => child.Visible))
        {
            child.Arrange(new GuiRectangle(
                childX,
                childY,
                Math.Min(innerWidth, child.DesiredSize.X),
                child.DesiredSize.Y));
            childY += child.DesiredSize.Y + Spacing;
        }
    }

    protected override void OnRender(IRenderContext2D context)
    {
        if (BackgroundColor.W > 0.0f) context.DrawScreenRectangle(Bounds.Position, Bounds.Size, BackgroundColor);

        foreach (var child in _children.ToArray()) child.Render(context);
    }

    private bool ContainsDescendant(IGuiElement candidate)
    {
        return _children.Any(child =>
            ReferenceEquals(child, candidate) ||
            child is GuiPanel panel && panel.ContainsDescendant(candidate));
    }
}
