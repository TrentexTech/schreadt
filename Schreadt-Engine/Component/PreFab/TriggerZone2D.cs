using Schreadt_Engine.Collision;
using Schreadt_Engine.Component.Logic;
using Schreadt_Engine.Core;
using Silk.NET.Maths;

namespace Schreadt_Engine.Component.PreFab;

public readonly record struct TriggerOverlap2D(
    GameObject Other,
    Collider2D OtherCollider,
    Vector2D<double> Normal,
    double Penetration);

public sealed class TriggerZone2D : Actor
{
    private readonly HashSet<Collider2D> _overlappingColliders = [];
    private Vector4D<float> _color = new(0.3f, 0.75f, 1.0f, 0.25f);

    public CircleCollider2D Collider { get; }

    public double Radius
    {
        get => Collider.Radius;
        set => Collider.Radius = value;
    }

    public bool Enabled
    {
        get => Collider.Enabled;
        set => Collider.Enabled = value;
    }

    public int CollisionLayer
    {
        get => Collider.CollisionLayer;
        set => Collider.CollisionLayer = value;
    }

    public CollisionLayerMask2D CollisionMask
    {
        get => Collider.CollisionMask;
        set => Collider.CollisionMask = value;
    }

    public bool Visible { get; set; } = true;

    public bool DetectOtherTriggers { get; set; }

    public Predicate<GameObject>? Filter { get; set; }

    public Vector4D<float> Color
    {
        get => _color;
        set
        {
            if (!float.IsFinite(value.X) || !float.IsFinite(value.Y) ||
                !float.IsFinite(value.Z) || !float.IsFinite(value.W))
                throw new ArgumentOutOfRangeException(nameof(value), "Trigger-zone color must be finite.");

            _color = value;
        }
    }

    public IReadOnlyCollection<GameObject> Occupants => _overlappingColliders
        .Select(collider => collider.Owner)
        .Distinct()
        .ToArray();

    public event Action<TriggerOverlap2D>? Entered;
    public event Action<TriggerOverlap2D>? Stayed;
    public event Action<TriggerOverlap2D>? Exited;

    public TriggerZone2D(double radius)
    {
        Collider = AddComponent(new CircleCollider2D(radius) { IsTrigger = true });
        Collider.CollisionEntered += HandleCollisionEntered;
        Collider.CollisionStayed += HandleCollisionStayed;
        Collider.CollisionExited += HandleCollisionExited;
    }

    public bool Contains(GameObject gameObject)
    {
        ArgumentNullException.ThrowIfNull(gameObject);
        return _overlappingColliders.Any(collider => ReferenceEquals(collider.Owner, gameObject));
    }

    protected override void OnRender(IRenderContext2D renderer)
    {
        if (Visible) renderer.DrawCircle(Position + Collider.Offset, Radius, Color);
    }

    private void HandleCollisionEntered(CollisionContact2D contact)
    {
        if (!Accepts(contact.Other)) return;

        if (_overlappingColliders.Add(contact.Other)) Entered?.Invoke(CreateOverlap(contact));
    }

    private void HandleCollisionStayed(CollisionContact2D contact)
    {
        if (!Accepts(contact.Other))
        {
            if (_overlappingColliders.Remove(contact.Other)) Exited?.Invoke(CreateOverlap(contact));
            return;
        }

        if (_overlappingColliders.Add(contact.Other)) Entered?.Invoke(CreateOverlap(contact));
        else Stayed?.Invoke(CreateOverlap(contact));
    }

    private void HandleCollisionExited(CollisionContact2D contact)
    {
        if (_overlappingColliders.Remove(contact.Other)) Exited?.Invoke(CreateOverlap(contact));
    }

    private bool Accepts(Collider2D other)
    {
        return (DetectOtherTriggers || !other.IsTrigger) && (Filter?.Invoke(other.Owner) ?? true);
    }

    private static TriggerOverlap2D CreateOverlap(CollisionContact2D contact)
    {
        return new TriggerOverlap2D(contact.Other.Owner, contact.Other, contact.Normal, contact.Penetration);
    }
}