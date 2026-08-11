using Schreadt_Engine.Core;
using Silk.NET.Maths;

namespace Schreadt_Engine.Collision;

public sealed class CollisionWorld2D
{
    private readonly record struct ShapePair(Type First, Type Second);

    private readonly List<Collider2D> _colliders = [];
    private readonly HashSet<RigidBody2D> _bodies = [];
    private readonly Dictionary<ShapePair, INarrowPhaseRegistration> _narrowPhases = [];
    private Dictionary<ColliderPair, CollisionManifold> _activePairs = [];
    private Vector2D<double> _gravity = new(0.0, -9.81);
    private int _lastPairCheckCount;
    private int _lastNarrowPhaseTestCount;

    public CollisionWorld2D()
    {
        RegisterNarrowPhase(new CircleCircleNarrowPhase2D());
        RegisterNarrowPhase(new BoxBoxNarrowPhase2D());
        RegisterNarrowPhase(new CircleBoxNarrowPhase2D());
    }

    public IReadOnlyList<Collider2D> Colliders => _colliders;

    public IReadOnlyCollection<RigidBody2D> Bodies => _bodies;

    public CollisionDebugDraw2D DebugDraw { get; } = new();

    public CollisionStatistics2D Statistics => new(
        _colliders.Count,
        _colliders.Count(CanCollide),
        _bodies.Count,
        _lastPairCheckCount,
        _lastNarrowPhaseTestCount,
        _activePairs.Count);

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

    public void RegisterNarrowPhase<TFirst, TSecond>(
        ICollisionNarrowPhase2D<TFirst, TSecond> narrowPhase,
        bool replaceExisting = false)
        where TFirst : Collider2D, ICollisionShape2D
        where TSecond : Collider2D, ICollisionShape2D
    {
        ArgumentNullException.ThrowIfNull(narrowPhase);

        var pair = new ShapePair(typeof(TFirst), typeof(TSecond));
        var reversePair = new ShapePair(typeof(TSecond), typeof(TFirst));
        var alreadyRegistered = _narrowPhases.ContainsKey(pair) || _narrowPhases.ContainsKey(reversePair);

        if (alreadyRegistered && !replaceExisting)
        {
            throw new InvalidOperationException(
                $"A narrow-phase handler is already registered for {typeof(TFirst).Name} and {typeof(TSecond).Name}.");
        }

        if (replaceExisting)
        {
            _narrowPhases.Remove(pair);
            _narrowPhases.Remove(reversePair);
        }

        _narrowPhases.Add(pair, new NarrowPhaseRegistration<TFirst, TSecond>(narrowPhase));
    }

    public bool UnregisterNarrowPhase<TFirst, TSecond>()
        where TFirst : Collider2D, ICollisionShape2D
        where TSecond : Collider2D, ICollisionShape2D
    {
        var pair = new ShapePair(typeof(TFirst), typeof(TSecond));
        var reversePair = new ShapePair(typeof(TSecond), typeof(TFirst));
        return _narrowPhases.Remove(pair) || _narrowPhases.Remove(reversePair);
    }

    public IReadOnlyList<Collider2D> OverlapPoint(
        Vector2D<double> point,
        CollisionQueryFilter2D? filter = null)
    {
        var results = new List<Collider2D>();
        OverlapPoint(point, results, filter);
        return results.AsReadOnly();
    }

    public int OverlapPoint(
        Vector2D<double> point,
        ICollection<Collider2D> results,
        CollisionQueryFilter2D? filter = null)
    {
        ValidatePoint(point, nameof(point));
        ArgumentNullException.ThrowIfNull(results);
        results.Clear();
        var queryFilter = filter ?? CollisionQueryFilter2D.All;

        foreach (var collider in _colliders)
        {
            if (!CanQuery(collider, queryFilter) || !ContainsPoint(collider, point)) continue;
            results.Add(collider);
        }

        return results.Count;
    }

    public IReadOnlyList<Collider2D> OverlapCircle(
        Vector2D<double> center,
        double radius,
        CollisionQueryFilter2D? filter = null)
    {
        var results = new List<Collider2D>();
        OverlapCircle(center, radius, results, filter);
        return results.AsReadOnly();
    }

    public int OverlapCircle(
        Vector2D<double> center,
        double radius,
        ICollection<Collider2D> results,
        CollisionQueryFilter2D? filter = null)
    {
        ValidatePoint(center, nameof(center));
        if (!double.IsFinite(radius) || radius < 0.0)
            throw new ArgumentOutOfRangeException(nameof(radius), "Query radius must be finite and non-negative.");
        ArgumentNullException.ThrowIfNull(results);
        results.Clear();
        var queryFilter = filter ?? CollisionQueryFilter2D.All;

        foreach (var collider in _colliders)
        {
            if (!CanQuery(collider, queryFilter) || !OverlapsCircle(collider, center, radius)) continue;
            results.Add(collider);
        }

        return results.Count;
    }

    public IReadOnlyList<Collider2D> OverlapBox(
        Vector2D<double> center,
        Vector2D<double> size,
        CollisionQueryFilter2D? filter = null)
    {
        var results = new List<Collider2D>();
        OverlapBox(center, size, results, filter);
        return results.AsReadOnly();
    }

    public int OverlapBox(
        Vector2D<double> center,
        Vector2D<double> size,
        ICollection<Collider2D> results,
        CollisionQueryFilter2D? filter = null)
    {
        ValidatePoint(center, nameof(center));
        if (!double.IsFinite(size.X) || !double.IsFinite(size.Y) || size.X <= 0.0 || size.Y <= 0.0)
            throw new ArgumentOutOfRangeException(nameof(size), "Query size must be finite and positive.");
        ArgumentNullException.ThrowIfNull(results);
        results.Clear();
        var queryFilter = filter ?? CollisionQueryFilter2D.All;
        var halfSize = size * 0.5;
        var minimum = center - halfSize;
        var maximum = center + halfSize;

        foreach (var collider in _colliders)
        {
            if (!CanQuery(collider, queryFilter) || !OverlapsBox(collider, minimum, maximum)) continue;
            results.Add(collider);
        }

        return results.Count;
    }

    public bool Raycast(
        Vector2D<double> origin,
        Vector2D<double> direction,
        double maximumDistance,
        out RaycastHit2D hit,
        CollisionQueryFilter2D? filter = null)
    {
        ValidateRay(origin, direction, maximumDistance, out var normalizedDirection);
        var queryFilter = filter ?? CollisionQueryFilter2D.All;
        var found = false;
        var nearestDistance = maximumDistance;
        hit = default;

        foreach (var collider in _colliders)
        {
            if (!CanQuery(collider, queryFilter) ||
                !TryRaycast(collider, origin, normalizedDirection, maximumDistance, out var distance, out var normal) ||
                (found && distance >= nearestDistance))
            {
                continue;
            }

            found = true;
            nearestDistance = distance;
            hit = new RaycastHit2D(
                collider,
                origin + (normalizedDirection * distance),
                normal,
                distance,
                maximumDistance);
        }

        return found;
    }

    public IReadOnlyList<RaycastHit2D> RaycastAll(
        Vector2D<double> origin,
        Vector2D<double> direction,
        double maximumDistance,
        CollisionQueryFilter2D? filter = null)
    {
        var results = new List<RaycastHit2D>();
        RaycastAll(origin, direction, maximumDistance, results, filter);
        return results.AsReadOnly();
    }

    public int RaycastAll(
        Vector2D<double> origin,
        Vector2D<double> direction,
        double maximumDistance,
        List<RaycastHit2D> results,
        CollisionQueryFilter2D? filter = null)
    {
        ValidateRay(origin, direction, maximumDistance, out var normalizedDirection);
        ArgumentNullException.ThrowIfNull(results);
        results.Clear();
        var queryFilter = filter ?? CollisionQueryFilter2D.All;

        foreach (var collider in _colliders)
        {
            if (!CanQuery(collider, queryFilter) ||
                !TryRaycast(collider, origin, normalizedDirection, maximumDistance, out var distance, out var normal))
            {
                continue;
            }

            results.Add(new RaycastHit2D(
                collider,
                origin + (normalizedDirection * distance),
                normal,
                distance,
                maximumDistance));
        }

        results.Sort(static (first, second) => first.Distance.CompareTo(second.Distance));
        return results.Count;
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

        _lastPairCheckCount = 0;
        _lastNarrowPhaseTestCount = 0;
        var currentPairs = new Dictionary<ColliderPair, CollisionManifold>();
        var colliders = _colliders.ToArray();

        for (var firstIndex = 0; firstIndex < colliders.Length; firstIndex++)
        {
            var first = colliders[firstIndex];
            if (!CanCollide(first)) continue;

            for (var secondIndex = firstIndex + 1; secondIndex < colliders.Length; secondIndex++)
            {
                var second = colliders[secondIndex];
                _lastPairCheckCount++;
                if (!CanCollide(second) || ReferenceEquals(first.Body, second.Body) || !first.CanCollideWith(second))
                    continue;

                _lastNarrowPhaseTestCount++;
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
        _lastPairCheckCount = 0;
        _lastNarrowPhaseTestCount = 0;
    }

    internal void DrawDiagnostics(IRenderContext2D renderer)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        DebugDraw.Draw(renderer, _colliders);
    }

    private bool CanCollide(Collider2D collider)
    {
        return ReferenceEquals(collider.World, this) && collider.Enabled && collider.Owner.ActiveInHierarchy;
    }

    private bool CanQuery(Collider2D collider, CollisionQueryFilter2D filter)
    {
        return CanCollide(collider) && filter.Allows(collider);
    }

    private static bool ContainsPoint(Collider2D collider, Vector2D<double> point)
    {
        return collider switch
        {
            CircleCollider2D circle => LengthSquared(point - circle.Center) <= circle.Radius * circle.Radius,
            AxisAlignedBoxCollider2D box => point.X >= box.Minimum.X && point.X <= box.Maximum.X &&
                                                  point.Y >= box.Minimum.Y && point.Y <= box.Maximum.Y,
            _ => false
        };
    }

    private static bool OverlapsCircle(Collider2D collider, Vector2D<double> center, double radius)
    {
        switch (collider)
        {
            case CircleCollider2D circle:
                var combinedRadius = radius + circle.Radius;
                return LengthSquared(center - circle.Center) <= combinedRadius * combinedRadius;

            case AxisAlignedBoxCollider2D box:
                var closest = new Vector2D<double>(
                    Math.Clamp(center.X, box.Minimum.X, box.Maximum.X),
                    Math.Clamp(center.Y, box.Minimum.Y, box.Maximum.Y));
                return LengthSquared(center - closest) <= radius * radius;

            default:
                return false;
        }
    }

    private static bool OverlapsBox(
        Collider2D collider,
        Vector2D<double> minimum,
        Vector2D<double> maximum)
    {
        switch (collider)
        {
            case CircleCollider2D circle:
                var closest = new Vector2D<double>(
                    Math.Clamp(circle.Center.X, minimum.X, maximum.X),
                    Math.Clamp(circle.Center.Y, minimum.Y, maximum.Y));
                return LengthSquared(circle.Center - closest) <= circle.Radius * circle.Radius;

            case AxisAlignedBoxCollider2D box:
                return minimum.X <= box.Maximum.X && maximum.X >= box.Minimum.X &&
                       minimum.Y <= box.Maximum.Y && maximum.Y >= box.Minimum.Y;

            default:
                return false;
        }
    }

    private static bool TryRaycast(
        Collider2D collider,
        Vector2D<double> origin,
        Vector2D<double> direction,
        double maximumDistance,
        out double distance,
        out Vector2D<double> normal)
    {
        return collider switch
        {
            CircleCollider2D circle => TryRaycastCircle(
                circle, origin, direction, maximumDistance, out distance, out normal),
            AxisAlignedBoxCollider2D box => TryRaycastBox(
                box, origin, direction, maximumDistance, out distance, out normal),
            _ => NoRaycastHit(out distance, out normal)
        };
    }

    private static bool TryRaycastCircle(
        CircleCollider2D circle,
        Vector2D<double> origin,
        Vector2D<double> direction,
        double maximumDistance,
        out double distance,
        out Vector2D<double> normal)
    {
        var offset = origin - circle.Center;
        var radiusSquared = circle.Radius * circle.Radius;
        if (LengthSquared(offset) <= radiusSquared)
        {
            distance = 0.0;
            normal = -direction;
            return true;
        }

        var projection = Dot(offset, direction);
        var discriminant = projection * projection - (LengthSquared(offset) - radiusSquared);
        if (discriminant < 0.0) return NoRaycastHit(out distance, out normal);

        distance = -projection - Math.Sqrt(discriminant);
        if (distance < 0.0 || distance > maximumDistance)
            return NoRaycastHit(out distance, out normal);

        normal = (origin + (direction * distance) - circle.Center) / circle.Radius;
        return true;
    }

    private static bool TryRaycastBox(
        AxisAlignedBoxCollider2D box,
        Vector2D<double> origin,
        Vector2D<double> direction,
        double maximumDistance,
        out double distance,
        out Vector2D<double> normal)
    {
        if (ContainsPoint(box, origin))
        {
            distance = 0.0;
            normal = -direction;
            return true;
        }

        var near = 0.0;
        var far = maximumDistance;
        normal = Vector2D<double>.Zero;
        if (!ClipRayAxis(
                origin.X, direction.X, box.Minimum.X, box.Maximum.X,
                new Vector2D<double>(-1.0, 0.0), new Vector2D<double>(1.0, 0.0),
                ref near, ref far, ref normal) ||
            !ClipRayAxis(
                origin.Y, direction.Y, box.Minimum.Y, box.Maximum.Y,
                new Vector2D<double>(0.0, -1.0), new Vector2D<double>(0.0, 1.0),
                ref near, ref far, ref normal) ||
            near > maximumDistance)
        {
            return NoRaycastHit(out distance, out normal);
        }

        distance = near;
        return true;
    }

    private static bool ClipRayAxis(
        double origin,
        double direction,
        double minimum,
        double maximum,
        Vector2D<double> minimumNormal,
        Vector2D<double> maximumNormal,
        ref double near,
        ref double far,
        ref Vector2D<double> hitNormal)
    {
        if (Math.Abs(direction) <= double.Epsilon)
            return origin >= minimum && origin <= maximum;

        double axisNear;
        double axisFar;
        Vector2D<double> axisNormal;
        if (direction > 0.0)
        {
            axisNear = (minimum - origin) / direction;
            axisFar = (maximum - origin) / direction;
            axisNormal = minimumNormal;
        }
        else
        {
            axisNear = (maximum - origin) / direction;
            axisFar = (minimum - origin) / direction;
            axisNormal = maximumNormal;
        }

        if (axisNear > near)
        {
            near = axisNear;
            hitNormal = axisNormal;
        }

        far = Math.Min(far, axisFar);
        return near <= far;
    }

    private static bool NoRaycastHit(out double distance, out Vector2D<double> normal)
    {
        distance = 0.0;
        normal = Vector2D<double>.Zero;
        return false;
    }

    private static void ValidatePoint(Vector2D<double> point, string parameterName)
    {
        if (!double.IsFinite(point.X) || !double.IsFinite(point.Y))
            throw new ArgumentOutOfRangeException(parameterName, "Query coordinates must be finite.");
    }

    private static void ValidateRay(
        Vector2D<double> origin,
        Vector2D<double> direction,
        double maximumDistance,
        out Vector2D<double> normalizedDirection)
    {
        ValidatePoint(origin, nameof(origin));
        ValidatePoint(direction, nameof(direction));
        if (!double.IsFinite(maximumDistance) || maximumDistance < 0.0)
            throw new ArgumentOutOfRangeException(nameof(maximumDistance), "Ray length must be finite and non-negative.");

        var lengthSquared = LengthSquared(direction);
        if (lengthSquared <= double.Epsilon)
            throw new ArgumentOutOfRangeException(nameof(direction), "Ray direction must have a non-zero length.");
        normalizedDirection = direction / Math.Sqrt(lengthSquared);
    }

    private static double LengthSquared(Vector2D<double> value) => Dot(value, value);

    private bool TryCreateManifold(
        Collider2D first,
        Collider2D second,
        out CollisionManifold manifold)
    {
        var pair = new ShapePair(first.GetType(), second.GetType());
        if (_narrowPhases.TryGetValue(pair, out var narrowPhase))
        {
            return TryCreateManifold(narrowPhase, first, second, reverseResult: false, out manifold);
        }

        var reversePair = new ShapePair(second.GetType(), first.GetType());
        if (_narrowPhases.TryGetValue(reversePair, out narrowPhase))
        {
            return TryCreateManifold(narrowPhase, second, first, reverseResult: true, out manifold);
        }

        manifold = default;
        return false;
    }

    private static bool TryCreateManifold(
        INarrowPhaseRegistration narrowPhase,
        Collider2D handlerFirst,
        Collider2D handlerSecond,
        bool reverseResult,
        out CollisionManifold manifold)
    {
        if (!narrowPhase.TryCollide(handlerFirst, handlerSecond, out var result))
        {
            manifold = default;
            return false;
        }

        ValidateResult(result, narrowPhase);
        manifold = reverseResult
            ? new CollisionManifold(handlerSecond, handlerFirst, -result.Normal, result.Penetration)
            : new CollisionManifold(handlerFirst, handlerSecond, result.Normal, result.Penetration);
        return true;
    }

    private static void ValidateResult(CollisionResult2D result, INarrowPhaseRegistration narrowPhase)
    {
        var normalLengthSquared = Dot(result.Normal, result.Normal);
        if (!double.IsFinite(normalLengthSquared) || Math.Abs(normalLengthSquared - 1.0) > 1e-10 ||
            !double.IsFinite(result.Penetration) || result.Penetration < 0.0)
        {
            throw new InvalidDataException(
                $"The narrow-phase handler for {narrowPhase.FirstType.Name} and {narrowPhase.SecondType.Name} returned an invalid collision result.");
        }
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

    private interface INarrowPhaseRegistration
    {
        Type FirstType { get; }
        Type SecondType { get; }

        bool TryCollide(Collider2D first, Collider2D second, out CollisionResult2D result);
    }

    private sealed class NarrowPhaseRegistration<TFirst, TSecond>(
        ICollisionNarrowPhase2D<TFirst, TSecond> narrowPhase) : INarrowPhaseRegistration
        where TFirst : Collider2D, ICollisionShape2D
        where TSecond : Collider2D, ICollisionShape2D
    {
        public Type FirstType => typeof(TFirst);
        public Type SecondType => typeof(TSecond);

        public bool TryCollide(Collider2D first, Collider2D second, out CollisionResult2D result)
        {
            return narrowPhase.TryCollide((TFirst)first, (TSecond)second, out result);
        }
    }
}
