using Schreadt_Engine.Component;
using Schreadt_Engine.Core;
using Silk.NET.Maths;

namespace Mandelbrot_Explorer.Logic;

internal sealed class MandelbrotCanvas : GameObject
{
    private const int PixelWidth = 1280;
    private const int PixelHeight = 720;
    private const double AspectRatio = (double)PixelWidth / PixelHeight;
    private static readonly MandelbrotView DefaultView = new(-0.5, 0.0, 3.5, 160);
    private static readonly MandelbrotView[] Landmarks =
    [
        DefaultView,
        new MandelbrotView(-0.743643887037151, 0.13182590420533, 0.0025, 224),
        new MandelbrotView(0.285, 0.01, 0.03, 224),
        new MandelbrotView(-0.77654, -0.136641, 0.00015, 288)
    ];

    private byte[] _pixels = [];
    private int _palette;

    internal MandelbrotView View { get; private set; } = DefaultView;
    internal string PaletteName => MandelbrotGenerator.PaletteNames[_palette];

    internal event Action? Changed;

    internal MandelbrotCanvas()
    {
        Regenerate();
    }

    internal void Reset()
    {
        View = DefaultView;
        Regenerate();
    }

    internal void Pan(double horizontalFraction, double verticalFraction)
    {
        View = View with
        {
            CenterX = View.CenterX + View.Width * horizontalFraction,
            CenterY = View.CenterY + View.Height(AspectRatio) * verticalFraction
        };
        Regenerate();
    }

    internal void ZoomAt(Vector2D<double> viewportPoint, double factor)
    {
        View = View.ZoomAt(viewportPoint, factor, AspectRatio);
        Regenerate();
    }

    internal void ChangeIterations(int amount)
    {
        var iterations = Math.Clamp(View.MaxIterations + amount, 32, 1024);
        if (iterations == View.MaxIterations) return;
        View = View with { MaxIterations = iterations };
        Regenerate();
    }

    internal void CyclePalette()
    {
        _palette = (_palette + 1) % MandelbrotGenerator.PaletteNames.Count;
        Regenerate();
    }

    internal void LoadLandmark(int index)
    {
        if ((uint)index >= Landmarks.Length) throw new ArgumentOutOfRangeException(nameof(index));
        View = Landmarks[index];
        Regenerate();
    }

    protected override void OnRender(IRenderContext2D renderer)
    {
        if (renderer is not IPixelRenderContext2D pixelRenderer)
            throw new NotSupportedException("The Mandelbrot explorer requires pixel-buffer rendering support.");

        pixelRenderer.DrawScreenPixels(_pixels, PixelWidth, PixelHeight, TextureSampling.Linear);
    }

    private void Regenerate()
    {
        _pixels = MandelbrotGenerator.Render(View, PixelWidth, PixelHeight, _palette);
        Changed?.Invoke();
    }
}

internal readonly record struct MandelbrotView(double CenterX, double CenterY, double Width, int MaxIterations)
{
    internal double Height(double aspectRatio) => Width / aspectRatio;

    internal Vector2D<double> ComplexPointAt(Vector2D<double> viewportPoint, double aspectRatio)
    {
        var x = Math.Clamp(viewportPoint.X, 0.0, 1.0);
        var y = Math.Clamp(viewportPoint.Y, 0.0, 1.0);
        return new Vector2D<double>(
            CenterX + (x - 0.5) * Width,
            CenterY + (y - 0.5) * Height(aspectRatio));
    }

    internal MandelbrotView ZoomAt(Vector2D<double> viewportPoint, double factor, double aspectRatio)
    {
        if (!double.IsFinite(factor) || factor <= 0.0) throw new ArgumentOutOfRangeException(nameof(factor));
        var target = ComplexPointAt(viewportPoint, aspectRatio);
        var nextWidth = Math.Clamp(Width * factor, 1e-13, 4.0);
        var appliedFactor = nextWidth / Width;
        return this with
        {
            CenterX = target.X + (CenterX - target.X) * appliedFactor,
            CenterY = target.Y + (CenterY - target.Y) * appliedFactor,
            Width = nextWidth
        };
    }
}

internal static class MandelbrotGenerator
{
    internal static IReadOnlyList<string> PaletteNames { get; } = ["NEON TIDE", "SOLAR FLARE", "AURORA"];

    internal static byte[] Render(MandelbrotView view, int width, int height, int palette)
    {
        if (width <= 1) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 1) throw new ArgumentOutOfRangeException(nameof(height));
        if ((uint)palette >= PaletteNames.Count) throw new ArgumentOutOfRangeException(nameof(palette));

        var pixels = new byte[checked(width * height * 4)];
        var viewHeight = view.Height((double)width / height);
        Parallel.For(0, height, pixelY =>
        {
            var imaginary = view.CenterY + (0.5 - (double)pixelY / (height - 1)) * viewHeight;
            for (var pixelX = 0; pixelX < width; pixelX++)
            {
                var real = view.CenterX + ((double)pixelX / (width - 1) - 0.5) * view.Width;
                var offset = (pixelY * width + pixelX) * 4;
                WritePixel(pixels, offset, real, imaginary, view.MaxIterations, palette);
            }
        });
        return pixels;
    }

    internal static int EscapeIterations(double real, double imaginary, int maxIterations, out double magnitudeSquared)
    {
        if (IsKnownInterior(real, imaginary))
        {
            magnitudeSquared = 0.0;
            return maxIterations;
        }

        var zReal = 0.0;
        var zImaginary = 0.0;
        magnitudeSquared = 0.0;
        for (var iteration = 0; iteration < maxIterations; iteration++)
        {
            var realSquared = zReal * zReal;
            var imaginarySquared = zImaginary * zImaginary;
            zImaginary = 2.0 * zReal * zImaginary + imaginary;
            zReal = realSquared - imaginarySquared + real;
            magnitudeSquared = zReal * zReal + zImaginary * zImaginary;
            if (magnitudeSquared > 4.0) return iteration + 1;
        }

        return maxIterations;
    }

    private static bool IsKnownInterior(double real, double imaginary)
    {
        var imaginarySquared = imaginary * imaginary;
        var cardioidX = real - 0.25;
        var cardioidQ = cardioidX * cardioidX + imaginarySquared;
        return cardioidQ * (cardioidQ + cardioidX) <= 0.25 * imaginarySquared ||
               (real + 1.0) * (real + 1.0) + imaginarySquared <= 0.0625;
    }

    private static void WritePixel(
        byte[] pixels,
        int offset,
        double real,
        double imaginary,
        int maxIterations,
        int palette)
    {
        var iterations = EscapeIterations(real, imaginary, maxIterations, out var magnitudeSquared);
        if (iterations >= maxIterations)
        {
            pixels[offset] = 3;
            pixels[offset + 1] = 5;
            pixels[offset + 2] = 14;
            pixels[offset + 3] = 255;
            return;
        }

        var smoothIteration = iterations + 1.0 - Math.Log2(Math.Log(Math.Sqrt(magnitudeSquared)));
        var t = Math.Clamp(smoothIteration / maxIterations, 0.0, 1.0);
        var (red, green, blue) = PaletteColor(t, palette);
        pixels[offset] = red;
        pixels[offset + 1] = green;
        pixels[offset + 2] = blue;
        pixels[offset + 3] = 255;
    }

    private static (byte Red, byte Green, byte Blue) PaletteColor(double t, int palette)
    {
        var wave = Math.Pow(t, 0.32);
        return palette switch
        {
            0 => (
                WaveByte(wave, 0.15, 0.85, 0.02),
                WaveByte(wave, 0.20, 0.78, 0.30),
                WaveByte(wave, 0.25, 0.75, 0.58)),
            1 => (
                WaveByte(wave, 0.35, 0.65, 0.06),
                WaveByte(wave, 0.18, 0.60, 0.88),
                WaveByte(wave, 0.12, 0.48, 0.70)),
            2 => (
                WaveByte(wave, 0.18, 0.70, 0.48),
                WaveByte(wave, 0.30, 0.66, 0.20),
                WaveByte(wave, 0.34, 0.64, 0.82)),
            _ => throw new ArgumentOutOfRangeException(nameof(palette))
        };
    }

    private static byte WaveByte(double t, double center, double amplitude, double phase)
    {
        var value = center + amplitude * (0.5 + 0.5 * Math.Cos(Math.Tau * (t + phase)));
        return (byte)Math.Round(Math.Clamp(value, 0.0, 1.0) * 255.0);
    }
}
