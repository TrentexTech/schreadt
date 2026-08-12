using Schreadt_Engine.Component.Logic;
using Silk.NET.Maths;

namespace Schreadt_Engine.Component.PreFab;

public sealed class Triangle : Polygon
{
    private static readonly Vector2D<double>[] DefaultVertices =
    [
        new(0.0, 0.5),
        new(-0.5, -0.5),
        new(0.5, -0.5)
    ];

    public Triangle() : base(DefaultVertices)
    {
    }

    public Triangle(
        Vector2D<double> first,
        Vector2D<double> second,
        Vector2D<double> third) : base([first, second, third])
    {
    }
}