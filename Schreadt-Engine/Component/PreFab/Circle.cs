using Schreadt_Engine.Component.Logic;
using Schreadt_Engine.Core;
using Silk.NET.Maths;

namespace Schreadt_Engine.Component.PreFab;

public class Circle : Actor
{
    public double Radius { get; set; } = 0.35;

    public Vector4D<float> Color { get; init; } = new(0.15f, 0.65f, 1.0f, 1.0f);

    public Circle()
    {
    }

    protected override void OnRender(IRenderContext2D renderer)
    {
        renderer.DrawCircle(Position, Radius, Color);
    }
}