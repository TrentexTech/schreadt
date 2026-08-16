using Schreadt_Engine.Gui;

namespace Schreadt_Engine.Tests.Gui;

public sealed class BitmapFontTests
{
    [Theory]
    [InlineData('+')]
    [InlineData('>')]
    [InlineData(';')]
    public void FoundryObjectivePunctuation_HasDedicatedGlyph(char character)
    {
        Assert.True(BitmapFont5x7.Supports(character));
        Assert.False(BitmapFont5x7.GetGlyph(character).SequenceEqual(BitmapFont5x7.GetGlyph('?')));
    }
}
