using Mandelbrot_Explorer.Logic;
using Silk.NET.Maths;

namespace Schreadt_Engine.Tests.Examples;

public sealed class MandelbrotExplorerTests
{
    [Theory]
    [InlineData(0.0, 0.0)]
    [InlineData(-1.0, 0.0)]
    [InlineData(-0.125, 0.744)]
    public void EscapeIterations_LeavesInteriorPointsBounded(double real, double imaginary)
    {
        const int maximum = 128;

        var iterations = MandelbrotGenerator.EscapeIterations(real, imaginary, maximum, out _);

        Assert.Equal(maximum, iterations);
    }

    [Fact]
    public void EscapeIterations_RecognizesEscapingPoint()
    {
        var iterations = MandelbrotGenerator.EscapeIterations(1.0, 1.0, 128, out var magnitudeSquared);

        Assert.InRange(iterations, 1, 127);
        Assert.True(magnitudeSquared > 4.0);
    }

    [Fact]
    public void Render_IsSymmetricAcrossTheRealAxis()
    {
        var pixels = MandelbrotGenerator.Render(new MandelbrotView(-0.5, 0.0, 3.5, 64), 31, 20, 0);
        const int rowLength = 31 * 4;

        for (var row = 0; row < 10; row++)
        {
            var top = pixels.AsSpan(row * rowLength, rowLength);
            var bottom = pixels.AsSpan((19 - row) * rowLength, rowLength);
            Assert.True(top.SequenceEqual(bottom));
        }
    }

    [Fact]
    public void ZoomAt_KeepsThePointUnderTheCursorFixed()
    {
        var view = new MandelbrotView(-0.5, 0.0, 3.5, 128);
        var cursor = new Vector2D<double>(0.72, 0.36);
        var target = view.ComplexPointAt(cursor, 16.0 / 9.0);

        var zoomed = view.ZoomAt(cursor, 0.4, 16.0 / 9.0);

        var zoomedTarget = zoomed.ComplexPointAt(cursor, 16.0 / 9.0);
        Assert.Equal(target.X, zoomedTarget.X, 12);
        Assert.Equal(target.Y, zoomedTarget.Y, 12);
    }
}
