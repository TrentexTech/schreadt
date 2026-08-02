using Silk.NET.Maths;

namespace Schreadt_Engine.Gui;

public sealed class GuiLabel
{
    private string _text;
    private float _scale = 2.0f;
    private float _padding = 6.0f;

    internal GuiLabel(string text)
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

    public Vector2D<float> Position { get; set; }

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

    public bool Visible { get; set; } = true;

    public Vector4D<float> Color { get; set; } = new(0.92f, 0.95f, 1.0f, 1.0f);

    public Vector4D<float> BackgroundColor { get; set; } = new(0.025f, 0.035f, 0.06f, 0.82f);
}
