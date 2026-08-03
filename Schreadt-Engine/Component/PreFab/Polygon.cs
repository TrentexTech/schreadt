using Schreadt_Engine.Component.Logic;
using Schreadt_Engine.Core;
using Silk.NET.Maths;

namespace Schreadt_Engine.Component.PreFab;

public class Polygon : Actor
{
    private readonly Vector2D<double>[] _vertices;
    private readonly IReadOnlyList<Vector2D<double>> _readOnlyVertices;
    private Vector2D<double> _scale = Vector2D<double>.One;
    private double _rotationRadians;

    public IReadOnlyList<Vector2D<double>> Vertices => _readOnlyVertices;
    public Vector2D<double> Scale
    {
        get => _scale;
        set
        {
            if (!double.IsFinite(value.X) || !double.IsFinite(value.Y) || value.X <= 0.0 || value.Y <= 0.0)
                throw new ArgumentOutOfRangeException(nameof(value), "Polygon scale must be finite and positive.");
            _scale = value;
        }
    }

    public double RotationRadians
    {
        get => _rotationRadians;
        set
        {
            if (!double.IsFinite(value))
                throw new ArgumentOutOfRangeException(nameof(value), "Polygon rotation must be finite.");
            _rotationRadians = value;
        }
    }

    public Vector4D<float> Color { get; set; } = new(0.7f, 0.35f, 1.0f, 1.0f);

    public Polygon(IEnumerable<Vector2D<double>> vertices, ActorLogic? actorLogic = null) : base(actorLogic)
    {
        _vertices = ConvexPolygon2D.CopyAndValidate(vertices, nameof(vertices));
        _readOnlyVertices = Array.AsReadOnly(_vertices);
    }

    protected override void OnRender(Renderer renderer)
    {
        renderer.DrawPolygon(Position, _vertices, Scale, RotationRadians, Color);
    }
}
