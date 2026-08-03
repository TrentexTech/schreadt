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
        if (_elements.Contains(element)) throw new InvalidOperationException("The GUI element is already registered.");
        if (Screens.Screens.Any(screen => ReferenceEquals(screen.Root, element)))
            throw new InvalidOperationException("The GUI element is already used as a screen root.");
        _elements.Add(element);
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
        return true;
    }

    public void Clear()
    {
        Screens.Clear();
        foreach (var element in _elements.ToArray()) _system?.ReleaseInteraction(element);
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
