using Silk.NET.Maths;

namespace Schreadt_Engine.Collision;

public sealed class CollisionWorld2D
{
    private readonly List<Collider2D> _colliders = [];
    private Dictionary<ColliderPair, CollisionManifold> _activePairs = [];

    public IReadOnlyList<Collider2D> Colliders => _colliders;

    public void AddCollider(Collider2D collider)
    {
        ArgumentNullException.ThrowIfNull(collider);

        if (collider.World is not null)
        {
            throw new InvalidOperationException("The collider already belongs to a collision world.");
        }

        collider.World = this;
        _colliders.Add(collider);
    }

    public bool RemoveCollider(Collider2D collider)
    {
        ArgumentNullException.ThrowIfNull(collider);

        if (!ReferenceEquals(collider.World, this) || !_colliders.Remove(collider)) return false;

        collider.World = null;
        return true;
    }

    internal void Step()
    {
        var currentPairs = new Dictionary<ColliderPair, CollisionManifold>();
        var colliders = _colliders.ToArray();

        for (var firstIndex = 0; firstIndex < colliders.Length; firstIndex++)
        {
            var first = colliders[firstIndex];
            if (!CanCollide(first)) continue;

            for (var secondIndex = firstIndex + 1; secondIndex < colliders.Length; secondIndex++)
            {
                var second = colliders[secondIndex];
                if (!CanCollide(second) || ReferenceEquals(first.Owner, second.Owner)) continue;

                if (!TryCreateManifold(first, second, out var manifold)) continue;

                Resolve(manifold);

                var pair = new ColliderPair(first.Id, second.Id);
                currentPairs.Add(pair, manifold);

                if (_activePairs.ContainsKey(pair))
                {
                    NotifyStayed(manifold);
                }
                else
                {
                    NotifyEntered(manifold);
                }
            }
        }

        foreach (var (pair, previousManifold) in _activePairs)
        {
            if (!currentPairs.ContainsKey(pair)) NotifyExited(previousManifold);
        }

        _activePairs = currentPairs;
    }

    internal void Clear()
    {
        foreach (var collider in _colliders)
        {
            collider.World = null;
        }

        _colliders.Clear();
        _activePairs.Clear();
    }

    private bool CanCollide(Collider2D collider)
    {
        return ReferenceEquals(collider.World, this) && collider.Enabled && collider.Owner.Active;
    }

    private static bool TryCreateManifold(
        Collider2D first,
        Collider2D second,
        out CollisionManifold manifold)
    {
        if (first is CircleCollider2D firstCircle && second is CircleCollider2D secondCircle)
        {
            var offset = secondCircle.Center - firstCircle.Center;
            var distanceSquared = offset.X * offset.X + offset.Y * offset.Y;
            var combinedRadius = firstCircle.Radius + secondCircle.Radius;

            if (distanceSquared > combinedRadius * combinedRadius)
            {
                manifold = default;
                return false;
            }

            var distance = Math.Sqrt(distanceSquared);
            var normal = distance > double.Epsilon
                ? offset / distance
                : new Vector2D<double>(1.0, 0.0);

            manifold = new CollisionManifold(
                firstCircle,
                secondCircle,
                normal,
                combinedRadius - distance);
            return true;
        }

        manifold = default;
        return false;
    }

    private static void Resolve(CollisionManifold manifold)
    {
        if (manifold.First.IsTrigger || manifold.Second.IsTrigger) return;

        var firstIsDynamic = manifold.First.BodyType == CollisionBodyType2D.Dynamic;
        var secondIsDynamic = manifold.Second.BodyType == CollisionBodyType2D.Dynamic;

        if (!firstIsDynamic && !secondIsDynamic) return;

        var firstShare = firstIsDynamic && secondIsDynamic ? 0.5 : firstIsDynamic ? 1.0 : 0.0;
        var secondShare = firstIsDynamic && secondIsDynamic ? 0.5 : secondIsDynamic ? 1.0 : 0.0;
        var correction = manifold.Normal * manifold.Penetration;

        if (firstShare > 0) manifold.First.Owner.Move(-correction * firstShare);
        if (secondShare > 0) manifold.Second.Owner.Move(correction * secondShare);
    }

    private static void NotifyEntered(CollisionManifold manifold)
    {
        manifold.First.NotifyEntered(CreateContactForFirst(manifold));
        manifold.Second.NotifyEntered(CreateContactForSecond(manifold));
    }

    private static void NotifyStayed(CollisionManifold manifold)
    {
        manifold.First.NotifyStayed(CreateContactForFirst(manifold));
        manifold.Second.NotifyStayed(CreateContactForSecond(manifold));
    }

    private static void NotifyExited(CollisionManifold manifold)
    {
        manifold.First.NotifyExited(CreateContactForFirst(manifold));
        manifold.Second.NotifyExited(CreateContactForSecond(manifold));
    }

    private static CollisionContact2D CreateContactForFirst(CollisionManifold manifold)
    {
        return new CollisionContact2D(manifold.Second, manifold.Normal, manifold.Penetration);
    }

    private static CollisionContact2D CreateContactForSecond(CollisionManifold manifold)
    {
        return new CollisionContact2D(manifold.First, -manifold.Normal, manifold.Penetration);
    }

    private readonly record struct ColliderPair(long FirstId, long SecondId);

    private readonly record struct CollisionManifold(
        Collider2D First,
        Collider2D Second,
        Vector2D<double> Normal,
        double Penetration);
}
