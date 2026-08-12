using Schreadt_Engine.Component.Logic;
using Schreadt_Engine.Core;
using Silk.NET.Maths;

namespace Schreadt_Engine.Component.PreFab;

public class Circle : Actor
{
    private static readonly Vector2D<double>[] UnitCircleVertices = Enumerable.Range(0, 64)
        .Select(index =>
        {
            var angle = index * Math.Tau / 64.0;
            return new Vector2D<double>(Math.Cos(angle), Math.Sin(angle));
        })
        .ToArray();

    public double Radius { get; set; } = 0.35;

    public Vector4D<float> Color { get; init; } = new(0.15f, 0.65f, 1.0f, 1.0f);

    public Circle()
    {
    }

    protected override void OnRender(IRenderContext2D renderer)
    {
        var worldScale = Transform.WorldScale;
        renderer.DrawPolygon(
            Position,
            UnitCircleVertices,
            new Vector2D<double>(Radius * worldScale.X, Radius * worldScale.Y),
            Transform.WorldRotation,
            Color);
    }
}
