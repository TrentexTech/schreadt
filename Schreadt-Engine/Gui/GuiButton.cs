using Schreadt_Engine.Core;
using Silk.NET.Maths;

namespace Schreadt_Engine.Gui;

public sealed class GuiButton : GuiControl
{
    private string _text;
    private float _scale = 2.0f;
    private float _padding = 8.0f;

    public GuiButton(string text)
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
            if (!float.IsFinite(value) || value <= 0.0f)
                throw new ArgumentOutOfRangeException(nameof(value), "Button scale must be finite and greater than zero.");

            _scale = value;
        }
    }

    public float Padding
    {
        get => _padding;
        set
        {
            if (!float.IsFinite(value) || value < 0.0f)
                throw new ArgumentOutOfRangeException(nameof(value), "Button padding must be finite and non-negative.");

            _padding = value;
        }
    }

    public Vector4D<float> TextColor { get; set; } = new(0.95f, 0.97f, 1.0f, 1.0f);

    public Vector4D<float> DisabledTextColor { get; set; } = new(0.55f, 0.58f, 0.65f, 1.0f);

    public Vector4D<float> BackgroundColor { get; set; } = new(0.12f, 0.18f, 0.3f, 0.95f);

    public Vector4D<float> HoveredBackgroundColor { get; set; } = new(0.18f, 0.32f, 0.52f, 0.98f);

    public Vector4D<float> PressedBackgroundColor { get; set; } = new(0.1f, 0.22f, 0.4f, 1.0f);

    public Vector4D<float> DisabledBackgroundColor { get; set; } = new(0.08f, 0.09f, 0.12f, 0.8f);

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
        var backgroundColor = !Enabled
            ? DisabledBackgroundColor
            : IsPressed
                ? PressedBackgroundColor
                : IsHovered
                    ? HoveredBackgroundColor
                    : BackgroundColor;
        var textColor = Enabled ? TextColor : DisabledTextColor;

        context.DrawScreenRectangle(Bounds.Position, Bounds.Size, backgroundColor);
        context.DrawText(Text, Bounds.Position, Scale, textColor, Vector4D<float>.Zero, Padding);
    }
}
