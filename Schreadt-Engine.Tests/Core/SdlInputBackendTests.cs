using System.Numerics;
using Schreadt_Engine.Core;
using Silk.NET.SDL;

namespace Schreadt_Engine.Tests.Core;

public sealed class SdlInputBackendTests
{
    [Theory]
    [InlineData(Scancode.ScancodeA, InputKey.A)]
    [InlineData(Scancode.Scancode0, InputKey.Number0)]
    [InlineData(Scancode.ScancodeReturn, InputKey.Enter)]
    [InlineData(Scancode.ScancodePageup, InputKey.PageUp)]
    [InlineData(Scancode.ScancodeKPPlus, InputKey.KeypadAdd)]
    [InlineData(Scancode.ScancodeLctrl, InputKey.ControlLeft)]
    [InlineData(Scancode.ScancodeRgui, InputKey.SuperRight)]
    [InlineData(Scancode.ScancodeApplication, InputKey.Menu)]
    [InlineData(Scancode.ScancodeUnknown, InputKey.Unknown)]
    public void TranslateScancode_MapsSdlPhysicalKeys(Scancode scancode, InputKey expected)
    {
        Assert.Equal(expected, InputManager.TranslateScancode(scancode));
    }

    [Theory]
    [InlineData(1, InputMouseButton.Left)]
    [InlineData(2, InputMouseButton.Middle)]
    [InlineData(3, InputMouseButton.Right)]
    [InlineData(4, InputMouseButton.Button4)]
    [InlineData(5, InputMouseButton.Button5)]
    [InlineData(12, InputMouseButton.Button12)]
    [InlineData(13, InputMouseButton.Unknown)]
    public void TranslateMouseButton_MapsSdlButtonNumbers(byte button, InputMouseButton expected)
    {
        Assert.Equal(expected, InputManager.TranslateMouseButton(button));
    }

    [Fact]
    public void ProcessFocusLost_ReleasesHeldInputAndActions()
    {
        using var input = new InputManager();
        input.SetActionBindings("move", InputBinding.ForKey(InputKey.W));
        input.ProcessKeyDown(InputKey.W);
        input.ProcessMouseDown(InputMouseButton.Left);

        input.ProcessFocusLost();

        Assert.False(input.IsKeyDown(InputKey.W));
        Assert.False(input.IsMouseButtonDown(InputMouseButton.Left));
        Assert.True(input.WasKeyReleased(InputKey.W));
        Assert.True(input.WasMouseButtonReleased(InputMouseButton.Left));
        Assert.True(input.WasActionReleased("move"));
    }

    [Fact]
    public void ProcessMouseMove_UsesSdlRelativeDelta()
    {
        using var input = new InputManager();

        input.ProcessMouseMove(new Vector2(100.0f, 50.0f), new Vector2(3.0f, -2.0f));

        Assert.Equal(new Vector2(100.0f, 50.0f), input.MousePosition);
        Assert.Equal(new Vector2(3.0f, -2.0f), input.MouseDelta);
    }
}
