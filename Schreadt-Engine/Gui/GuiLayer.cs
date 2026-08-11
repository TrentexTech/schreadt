namespace Schreadt_Engine.Gui;

public sealed class GuiLayer
{
    private readonly List<IGuiElement> _elements = [];
    private GuiSystem? _system;

    public GuiLayer()
    {
        Screens = new GuiScreenStack(this);
    }

    public IReadOnlyList<IGuiElement> Elements => _elements;

    public GuiScreenStack Screens { get; }

    public bool Visible { get; set; } = true;

    public bool InputEnabled { get; set; } = true;

    public bool Attached => _system is not null;

    public T Add<T>(T element) where T : IGuiElement
    {
        ArgumentNullException.ThrowIfNull(element);
        GuiElementOwnership.Claim(element, this, "GUI layer");
        try
        {
            _elements.Add(element);
        }
        catch
        {
            GuiElementOwnership.Release(element, this);
            throw;
        }

        return element;
    }

    public GuiLabel AddLabel(string text) => Add(new GuiLabel(text));

    public GuiPanel AddPanel() => Add(new GuiPanel());

    public GuiButton AddButton(string text) => Add(new GuiButton(text));

    public bool Remove(IGuiElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (!_elements.Remove(element)) return false;
        _system?.ReleaseInteraction(element);
        GuiElementOwnership.Release(element, this);
        return true;
    }

    public void Clear()
    {
        Screens.Clear();
        foreach (var element in _elements)
        {
            _system?.ReleaseInteraction(element);
            GuiElementOwnership.Release(element, this);
        }

        _elements.Clear();
    }

    internal void Attach(GuiSystem system)
    {
        ArgumentNullException.ThrowIfNull(system);
        if (_system is not null) throw new InvalidOperationException("The GUI layer is already attached to a GUI system.");
        _system = system;
    }

    internal void Detach(GuiSystem system)
    {
        if (!ReferenceEquals(_system, system)) return;
        foreach (var root in EnumerateRoots()) _system.ReleaseInteraction(root);
        _system = null;
    }

    internal IEnumerable<IGuiElement> EnumerateRoots()
    {
        foreach (var element in _elements) yield return element;
        foreach (var screen in Screens.Screens) yield return screen.Root;
    }

    internal void ReleaseInteraction(IGuiElement root) => _system?.ReleaseInteraction(root);
}
