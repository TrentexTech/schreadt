using Silk.NET.Maths;

using Schreadt_Engine.Core;

namespace Schreadt_Engine.Gui;

public sealed class GuiLabel : GuiElement
{
    private string _text;
    private float _scale = 2.0f;
    private float _padding = 6.0f;

    public GuiLabel(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        _text = text;
    }

    public string Text
    {
        get => _text;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _text = value;
        }
    }

    public float Scale
    {
        get => _scale;
        set
        {
            if (!float.IsFinite(value) || value <= 0)
                throw new ArgumentOutOfRangeException(nameof(value), "GUI scale must be finite and greater than zero.");

            _scale = value;
        }
    }

    public float Padding
    {
        get => _padding;
        set
        {
            if (!float.IsFinite(value) || value < 0)
                throw new ArgumentOutOfRangeException(nameof(value), "GUI padding must be finite and non-negative.");

            _padding = value;
        }
    }

    public Vector4D<float> Color { get; set; } = new(0.92f, 0.95f, 1.0f, 1.0f);

    public Vector4D<float> BackgroundColor { get; set; } = new(0.025f, 0.035f, 0.06f, 0.82f);

    protected override Vector2D<float> OnMeasure(Vector2D<float> availableSize)
    {
        var lines = Text.Replace("\r", string.Empty).Split('\n');
        var longestLine = lines.Max(line => line.Length);
        var textWidth = Math.Max(0.0f, (longestLine * BitmapFont5x7.CharacterAdvance - 1) * Scale);
        var textHeight = Math.Max(0.0f, (lines.Length * BitmapFont5x7.LineAdvance - 1) * Scale);
        return new Vector2D<float>(textWidth + Padding * 2.0f, textHeight + Padding * 2.0f);
    }

    protected override void OnRender(IRenderContext2D context)
    {
        context.DrawText(Text, Bounds.Position, Scale, Color, BackgroundColor, Padding);
    }
}
