using Schreadt_Engine.Gui;

namespace Schreadt_Engine.Tests.Gui;

public sealed class GuiOwnershipTests
{
    [Fact]
    public void DuplicateInsertion_IsRejectedWithoutChangingTheOwner()
    {
        var panel = new GuiPanel();
        var button = panel.AddButton("PLAY");

        var exception = Assert.Throws<InvalidOperationException>(() => panel.Add(button));

        Assert.Contains("already owned by GUI panel", exception.Message);
        Assert.Contains("Remove it", exception.Message);
        Assert.Single(panel.Children);
        Assert.Same(button, panel.Children[0]);
    }

    [Fact]
    public void CrossPanelInsertion_IdentifiesBothOwnersAndKeepsTheOriginalOwner()
    {
        var first = new GuiPanel();
        var second = new GuiPanel();
        var button = first.AddButton("PLAY");

        var exception = Assert.Throws<InvalidOperationException>(() => second.Add(button));

        Assert.Contains("cannot be added to GUI panel", exception.Message);
        Assert.Contains("already owned by GUI panel", exception.Message);
        Assert.Single(first.Children);
        Assert.Empty(second.Children);
    }

    [Fact]
    public void RemoveThenAdd_LegallyReparentsAnElement()
    {
        var first = new GuiPanel();
        var second = new GuiPanel();
        var button = first.AddButton("PLAY");

        Assert.True(first.Remove(button));
        Assert.Same(button, second.Add(button));

        Assert.Empty(first.Children);
        Assert.Same(button, Assert.Single(second.Children));
    }

    [Fact]
    public void Clear_ReleasesEveryChildForReparenting()
    {
        var first = new GuiPanel();
        var second = new GuiPanel();
        var firstButton = first.AddButton("FIRST");
        var secondButton = first.AddButton("SECOND");

        first.Clear();
        second.Add(firstButton);
        second.Add(secondButton);

        Assert.Empty(first.Children);
        Assert.Equal([firstButton, secondButton], second.Children);
    }

    [Fact]
    public void SceneAndPersistentCollections_EnforceTheSameOwnershipRule()
    {
        var gui = new GuiSystem();
        var layer = new GuiLayer();
        var button = layer.AddButton("PLAY");

        var exception = Assert.Throws<InvalidOperationException>(() => gui.Add(button));

        Assert.Contains("persistent GUI collection", exception.Message);
        Assert.Contains("already owned by GUI layer", exception.Message);
        Assert.True(layer.Remove(button));
        Assert.Same(button, gui.Add(button));
    }

    [Fact]
    public void PoppedScreenRoot_CanBeAddedToAnotherGuiCollection()
    {
        var gui = new GuiSystem();
        var layer = new GuiLayer();
        var root = new GuiPanel();
        var screen = layer.Screens.Push(new GuiScreen("menu", root));

        var exception = Assert.Throws<InvalidOperationException>(() => gui.Add(root));
        Assert.Contains("already owned by GUI screen 'menu'", exception.Message);

        Assert.Same(screen, layer.Screens.Pop());
        Assert.Same(root, gui.Add(root));
    }
}
