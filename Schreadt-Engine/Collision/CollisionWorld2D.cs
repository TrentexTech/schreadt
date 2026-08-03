using Silk.NET.Maths;

namespace Schreadt_Engine.Collision;

public sealed class CollisionWorld2D
{
    private readonly List<Collider2D> _colliders = [];
    private readonly HashSet<RigidBody2D> _bodies = [];
    private Dictionary<ColliderPair, CollisionManifold> _activePairs = [];
    private Vector2D<double> _gravity = new(0.0, -9.81);

    public IReadOnlyList<Collider2D> Colliders => _colliders;

    public IReadOnlyCollection<RigidBody2D> Bodies => _bodies;

    public Vector2D<double> Gravity
    {
        get => _gravity;
        set
        {
            if (!double.IsFinite(value.X) || !double.IsFinite(value.Y))
                throw new ArgumentOutOfRangeException(nameof(value), "Gravity components must be finite.");

            _gravity = value;
        }
    }

    internal void AddCollider(Collider2D collider)
    {
        ArgumentNullException.ThrowIfNull(collider);

        if (collider.World is not null)
        {
            throw new InvalidOperationException("The collider already belongs to a collision world.");
        }

        if (collider.Body.World is not null && !ReferenceEquals(collider.Body.World, this))
        {
            throw new InvalidOperationException("The collider's rigid body belongs to another collision world.");
        }

        collider.World = this;
        collider.Body.World = this;
        _colliders.Add(collider);
        _bodies.Add(collider.Body);
    }

    internal bool RemoveCollider(Collider2D collider)
    {
        ArgumentNullException.ThrowIfNull(collider);

        if (!ReferenceEquals(collider.World, this) || !_colliders.Remove(collider)) return false;

        var endedPairs = _activePairs
            .Where(pair => pair.Key.Contains(collider.Id))
            .ToArray();
        foreach (var (pair, _) in endedPairs) _activePairs.Remove(pair);
        foreach (var (_, manifold) in endedPairs) NotifyExited(manifold);

        collider.World = null;

        if (!_colliders.Any(candidate => ReferenceEquals(candidate.Body, collider.Body)))
        {
            _bodies.Remove(collider.Body);
            collider.Body.World = null;
        }

        return true;
    }

    internal void Step(double dt)
    {
        if (!double.IsFinite(dt) || dt < 0)
            throw new ArgumentOutOfRangeException(nameof(dt), "Delta time must be finite and non-negative.");

        foreach (var body in _bodies.ToArray())
        {
            if (ReferenceEquals(body.World, this)) body.Integrate(Gravity, dt);
        }

        var currentPairs = new Dictionary<ColliderPair, CollisionManifold>();
        var colliders = _colliders.ToArray();

        for (var firstIndex = 0; firstIndex < colliders.Length; firstIndex++)
        {
            var first = colliders[firstIndex];
            if (!CanCollide(first)) continue;

            for (var secondIndex = firstIndex + 1; secondIndex < colliders.Length; secondIndex++)
            {
                var second = colliders[secondIndex];
                if (!CanCollide(second) || ReferenceEquals(first.Body, second.Body)) continue;

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

        foreach (var body in _bodies.ToArray())
        {
            if (ReferenceEquals(body.World, this)) body.EndPhysicsStep(dt);
        }
    }

    internal void Clear()
    {
        var endedManifolds = _activePairs.Values.ToArray();
        _activePairs.Clear();
        foreach (var manifold in endedManifolds) NotifyExited(manifold);

        foreach (var collider in _colliders)
        {
            collider.World = null;
        }

        foreach (var body in _bodies)
        {
            body.World = null;
        }

        _colliders.Clear();
        _bodies.Clear();
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

        var firstBody = manifold.First.Body;
        var secondBody = manifold.Second.Body;
        var (firstCorrectionShare, secondCorrectionShare) = GetCorrectionShares(firstBody, secondBody);
        var correction = manifold.Normal * manifold.Penetration;

        if (firstCorrectionShare > 0)
            firstBody.ApplyPositionCorrection(-correction * firstCorrectionShare);
        if (secondCorrectionShare > 0)
            secondBody.ApplyPositionCorrection(correction * secondCorrectionShare);

        var firstInverseMass = firstBody.InverseMass;
        var secondInverseMass = secondBody.InverseMass;
        var totalInverseMass = firstInverseMass + secondInverseMass;

        if (totalInverseMass <= 0) return;

        var relativeVelocity = secondBody.Velocity - firstBody.Velocity;
        var velocityAlongNormal = relativeVelocity.X * manifold.Normal.X
                                  + relativeVelocity.Y * manifold.Normal.Y;

        if (velocityAlongNormal >= 0) return;

        var restitution = Math.Max(firstBody.Restitution, secondBody.Restitution);
        var normalImpulseMagnitude = -(1.0 + restitution) * velocityAlongNormal / totalInverseMass;
        var normalImpulse = manifold.Normal * normalImpulseMagnitude;

        firstBody.ApplyCollisionImpulse(-normalImpulse);
        secondBody.ApplyCollisionImpulse(normalImpulse);

        relativeVelocity = secondBody.Velocity - firstBody.Velocity;
        var remainingNormalVelocity = Dot(relativeVelocity, manifold.Normal);
        var tangentVelocity = relativeVelocity - manifold.Normal * remainingNormalVelocity;
        var tangentLengthSquared = Dot(tangentVelocity, tangentVelocity);

        if (tangentLengthSquared <= double.Epsilon) return;

        var tangent = tangentVelocity / Math.Sqrt(tangentLengthSquared);
        var tangentImpulseMagnitude = -Dot(relativeVelocity, tangent) / totalInverseMass;
        var combinedFriction = Math.Sqrt(firstBody.Friction * secondBody.Friction);
        var maximumFrictionImpulse = normalImpulseMagnitude * combinedFriction;
        tangentImpulseMagnitude = Math.Clamp(
            tangentImpulseMagnitude,
            -maximumFrictionImpulse,
            maximumFrictionImpulse);
        var frictionImpulse = tangent * tangentImpulseMagnitude;

        firstBody.ApplyCollisionImpulse(-frictionImpulse);
        secondBody.ApplyCollisionImpulse(frictionImpulse);
    }

    private static (double First, double Second) GetCorrectionShares(
        RigidBody2D first,
        RigidBody2D second)
    {
        if (first.BodyType == CollisionBodyType2D.Static)
        {
            return second.BodyType == CollisionBodyType2D.Static ? (0.0, 0.0) : (0.0, 1.0);
        }

        if (second.BodyType == CollisionBodyType2D.Static) return (1.0, 0.0);

        if (first.BodyType == CollisionBodyType2D.Kinematic)
        {
            return second.BodyType == CollisionBodyType2D.Dynamic ? (0.0, 1.0) : (0.0, 0.0);
        }

        if (second.BodyType == CollisionBodyType2D.Kinematic) return (1.0, 0.0);

        var firstInverseMass = first.InverseMass;
        var secondInverseMass = second.InverseMass;
        var totalInverseMass = firstInverseMass + secondInverseMass;
        return totalInverseMass > 0
            ? (firstInverseMass / totalInverseMass, secondInverseMass / totalInverseMass)
            : (0.0, 0.0);
    }

    private static double Dot(Vector2D<double> first, Vector2D<double> second)
    {
        return first.X * second.X + first.Y * second.Y;
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

    private readonly record struct ColliderPair(long FirstId, long SecondId)
    {
        internal bool Contains(long colliderId) => FirstId == colliderId || SecondId == colliderId;
    }

    private readonly record struct CollisionManifold(
        Collider2D First,
        Collider2D Second,
        Vector2D<double> Normal,
        double Penetration);
}
