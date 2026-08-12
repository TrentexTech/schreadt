using Schreadt_Engine.Component.Logic;
using Schreadt_Engine.Core;
using Silk.NET.Maths;

namespace Schreadt_Engine.Component.PreFab;

public class Polygon : Actor
{
    private readonly Vector2D<double>[] _vertices;
    private readonly IReadOnlyList<Vector2D<double>> _readOnlyVertices;

    public IReadOnlyList<Vector2D<double>> Vertices => _readOnlyVertices;

    public Vector2D<double> Scale
    {
        get => Transform.WorldScale;
        set => Transform.SetWorldScale(value);
    }

    public double RotationRadians
    {
        get => Transform.WorldRotation;
        set => Transform.SetWorldRotation(value);
    }

    public Vector4D<float> Color { get; set; } = new(0.7f, 0.35f, 1.0f, 1.0f);

    public Polygon(IEnumerable<Vector2D<double>> vertices)
    {
        _vertices = ConvexPolygon2D.CopyAndValidate(vertices, nameof(vertices));
        _readOnlyVertices = Array.AsReadOnly(_vertices);
    }

    protected override void OnRender(IRenderContext2D renderer)
    {
        renderer.DrawPolygon(Position, _vertices, Transform.WorldScale, Transform.WorldRotation, Color);
    }
}
