using System.Diagnostics;
using Schreadt_Engine.Core;
using Silk.NET.Maths;

namespace Schreadt_Engine.Collision;

public sealed class CollisionWorld2D
{
    private const int MinimumSolverIterationCount = 1;
    private const int MaximumSolverIterationCount = 32;
    private const double SleepingContactWakeSpeed = 0.25;
    private const double SleepingContactWakePenetration = 0.02;
    private const double PositionCorrectionSlop = 0.005;
    private const double PositionCorrectionFraction = 0.8;
    private const double RestitutionVelocityThreshold = 1.0;

    private readonly record struct ShapePair(Type First, Type Second);

    private readonly List<Collider2D> _colliders = [];
    private readonly HashSet<RigidBody2D> _bodies = [];
    private readonly Dictionary<ShapePair, INarrowPhaseRegistration> _narrowPhases = [];
    private Dictionary<ColliderPair, CollisionManifold> _activePairs = [];
    private Vector2D<double> _gravity = new(0.0, -9.81);
    private int _lastPairCheckCount;
    private int _lastNarrowPhaseTestCount;
    private int _positionIterations = 3;
    private int _velocityIterations = 8;
    private double _lastSolverMilliseconds;

    public CollisionWorld2D()
    {
        RegisterNarrowPhase(new CircleCircleNarrowPhase2D());
        RegisterNarrowPhase(new BoxBoxNarrowPhase2D());
        RegisterNarrowPhase(new CircleBoxNarrowPhase2D());
        RegisterNarrowPhase(new OrientedBoxOrientedBoxNarrowPhase2D());
        RegisterNarrowPhase(new AxisAlignedBoxOrientedBoxNarrowPhase2D());
        RegisterNarrowPhase(new CircleOrientedBoxNarrowPhase2D());
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
        _activePairs.Count,
        _activePairs.Values.Sum(static manifold => manifold.ContactPointCount),
        PositionIterations,
        VelocityIterations,
        _lastSolverMilliseconds);

    /// <summary>Number of deterministic penetration-correction passes performed per fixed step.</summary>
    public int PositionIterations
    {
        get => _positionIterations;
        set => _positionIterations = ValidateSolverIterationCount(value, nameof(value));
    }

    /// <summary>Number of deterministic sequential-impulse passes performed per fixed step.</summary>
    public int VelocityIterations
    {
        get => _velocityIterations;
        set => _velocityIterations = ValidateSolverIterationCount(value, nameof(value));
    }

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
        var currentPairOrder = new List<ColliderPair>();
        var currentManifolds = new List<CollisionManifold>();
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

                var pair = new ColliderPair(first.Id, second.Id);
                currentPairs.Add(pair, manifold);
                currentPairOrder.Add(pair);
                currentManifolds.Add(manifold);
            }
        }

        var solverTimer = Stopwatch.StartNew();
        WakeSleepingBodiesForMeaningfulContacts(currentManifolds);
        var positionIterationShare = 1.0 / PositionIterations;
        for (var iteration = 0; iteration < PositionIterations; iteration++)
        {
            foreach (var manifold in currentManifolds)
                ResolvePosition(manifold, positionIterationShare);
        }

        var velocityConstraints = currentManifolds
            .Select(CreateVelocityConstraint)
            .ToArray();
        for (var iteration = 0; iteration < VelocityIterations; iteration++)
        {
            foreach (var constraint in velocityConstraints)
                ResolveVelocity(constraint);
        }

        solverTimer.Stop();
        _lastSolverMilliseconds = solverTimer.Elapsed.TotalMilliseconds;

        foreach (var pair in currentPairOrder)
        {
            var manifold = currentPairs[pair];
            if (_activePairs.ContainsKey(pair)) NotifyStayed(manifold);
            else NotifyEntered(manifold);
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
        _lastSolverMilliseconds = 0.0;
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
            OrientedBoxCollider2D box =>
                BoxCollisionGeometry2D.ContainsPoint(BoxGeometry2D.From(box), point),
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

            case OrientedBoxCollider2D box:
                return BoxCollisionGeometry2D.OverlapsCircle(BoxGeometry2D.From(box), center, radius);

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

            case OrientedBoxCollider2D box:
                var queryCenter = (minimum + maximum) * 0.5;
                var queryHalfSize = (maximum - minimum) * 0.5;
                return BoxCollisionGeometry2D.TryCollide(
                    BoxGeometry2D.AxisAligned(queryCenter, queryHalfSize),
                    BoxGeometry2D.From(box),
                    out _);

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
            OrientedBoxCollider2D box => TryRaycastOrientedBox(
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

    private static bool TryRaycastOrientedBox(
        OrientedBoxCollider2D box,
        Vector2D<double> origin,
        Vector2D<double> direction,
        double maximumDistance,
        out double distance,
        out Vector2D<double> normal)
    {
        var geometry = BoxGeometry2D.From(box);
        if (BoxCollisionGeometry2D.ContainsPoint(geometry, origin))
        {
            distance = 0.0;
            normal = -direction;
            return true;
        }

        var originOffset = origin - geometry.Center;
        var localOrigin = new Vector2D<double>(
            Dot(originOffset, geometry.AxisX),
            Dot(originOffset, geometry.AxisY));
        var localDirection = new Vector2D<double>(
            Dot(direction, geometry.AxisX),
            Dot(direction, geometry.AxisY));
        var near = 0.0;
        var far = maximumDistance;
        var localNormal = Vector2D<double>.Zero;

        if (!ClipRayAxis(
                localOrigin.X, localDirection.X, -geometry.HalfSize.X, geometry.HalfSize.X,
                new Vector2D<double>(-1.0, 0.0), new Vector2D<double>(1.0, 0.0),
                ref near, ref far, ref localNormal) ||
            !ClipRayAxis(
                localOrigin.Y, localDirection.Y, -geometry.HalfSize.Y, geometry.HalfSize.Y,
                new Vector2D<double>(0.0, -1.0), new Vector2D<double>(0.0, 1.0),
                ref near, ref far, ref localNormal) ||
            near > maximumDistance)
        {
            return NoRaycastHit(out distance, out normal);
        }

        distance = near;
        normal = (geometry.AxisX * localNormal.X) + (geometry.AxisY * localNormal.Y);
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
        var firstContactPoint = result.ContactPointCount >= 1
            ? result.GetContactPoint(0)
            : (handlerFirst.Center + handlerSecond.Center) * 0.5;
        var secondContactPoint = result.ContactPointCount >= 2
            ? result.GetContactPoint(1)
            : default;
        var contactPointCount = Math.Max(1, result.ContactPointCount);
        manifold = reverseResult
            ? new CollisionManifold(
                handlerSecond,
                handlerFirst,
                -result.Normal,
                result.Penetration,
                contactPointCount,
                firstContactPoint,
                secondContactPoint)
            : new CollisionManifold(
                handlerFirst,
                handlerSecond,
                result.Normal,
                result.Penetration,
                contactPointCount,
                firstContactPoint,
                secondContactPoint);
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

    private static void ResolvePosition(CollisionManifold manifold, double iterationShare)
    {
        if (manifold.First.IsTrigger || manifold.Second.IsTrigger) return;

        var firstBody = manifold.First.Body;
        var secondBody = manifold.Second.Body;
        var correctedPenetration = Math.Max(manifold.Penetration - PositionCorrectionSlop, 0.0) *
                                   PositionCorrectionFraction;
        if (correctedPenetration <= 0.0) return;

        var correctionPerContact = correctedPenetration * iterationShare / manifold.ContactPointCount;
        for (var index = 0; index < manifold.ContactPointCount; index++)
        {
            var contactPoint = manifold.GetContactPoint(index);
            var firstRadius = contactPoint - firstBody.Owner.Position;
            var secondRadius = contactPoint - secondBody.Owner.Position;
            var firstInverseMass = GetSolverInverseMass(firstBody);
            var secondInverseMass = GetSolverInverseMass(secondBody);
            var firstLever = Cross(firstRadius, manifold.Normal);
            var secondLever = Cross(secondRadius, manifold.Normal);
            var denominator = firstInverseMass + secondInverseMass +
                              firstLever * firstLever * GetSolverInverseMomentOfInertia(firstBody) +
                              secondLever * secondLever * GetSolverInverseMomentOfInertia(secondBody);
            if (denominator <= 0.0) continue;

            var correctionImpulse = manifold.Normal * (correctionPerContact / denominator);
            firstBody.ApplyPositionImpulseAtPoint(-correctionImpulse, contactPoint);
            secondBody.ApplyPositionImpulseAtPoint(correctionImpulse, contactPoint);
        }
    }

    private static void WakeSleepingBodiesForMeaningfulContacts(
        IReadOnlyList<CollisionManifold> manifolds)
    {
        foreach (var manifold in manifolds)
        {
            if (manifold.First.IsTrigger || manifold.Second.IsTrigger) continue;

            var firstBody = manifold.First.Body;
            var secondBody = manifold.Second.Body;
            var firstWasSleeping = firstBody.BodyType == CollisionBodyType2D.Dynamic && firstBody.IsSleeping;
            var secondWasSleeping = secondBody.BodyType == CollisionBodyType2D.Dynamic && secondBody.IsSleeping;
            if (firstWasSleeping == secondWasSleeping) continue;

            var meaningfulPenetration = manifold.Penetration > SleepingContactWakePenetration;
            var meaningfulVelocity = false;
            for (var index = 0; index < manifold.ContactPointCount && !meaningfulVelocity; index++)
            {
                var contactPoint = manifold.GetContactPoint(index);
                var relativeVelocity = secondBody.GetVelocityAtPoint(contactPoint) -
                                       firstBody.GetVelocityAtPoint(contactPoint);
                meaningfulVelocity = Dot(relativeVelocity, relativeVelocity) >
                                     SleepingContactWakeSpeed * SleepingContactWakeSpeed;
            }

            if (!meaningfulPenetration && !meaningfulVelocity) continue;
            if (firstWasSleeping) firstBody.WakeUp();
            if (secondWasSleeping) secondBody.WakeUp();
        }
    }

    private static VelocityConstraint CreateVelocityConstraint(CollisionManifold manifold)
    {
        var constraint = new VelocityConstraint(manifold);
        if (manifold.First.IsTrigger || manifold.Second.IsTrigger) return constraint;

        constraint.FirstContact.RestitutionVelocity = CalculateRestitutionVelocity(
            manifold,
            manifold.GetContactPoint(0));
        if (manifold.ContactPointCount >= 2)
        {
            constraint.SecondContact.RestitutionVelocity = CalculateRestitutionVelocity(
                manifold,
                manifold.GetContactPoint(1));
        }

        return constraint;
    }

    private static double CalculateRestitutionVelocity(
        CollisionManifold manifold,
        Vector2D<double> contactPoint)
    {
        var relativeVelocity = manifold.Second.Body.GetVelocityAtPoint(contactPoint) -
                               manifold.First.Body.GetVelocityAtPoint(contactPoint);
        var velocityAlongNormal = Dot(relativeVelocity, manifold.Normal);
        if (velocityAlongNormal >= -RestitutionVelocityThreshold) return 0.0;

        var restitution = Math.Max(manifold.First.Body.Restitution, manifold.Second.Body.Restitution);
        return -restitution * velocityAlongNormal;
    }

    private static void ResolveVelocity(VelocityConstraint constraint)
    {
        var manifold = constraint.Manifold;
        if (manifold.First.IsTrigger || manifold.Second.IsTrigger) return;

        if (manifold.ContactPointCount >= 2 && !TryResolveNormalBlock(constraint))
        {
            ResolveNormalAtContact(
                manifold,
                manifold.GetContactPoint(0),
                ref constraint.FirstContact);
            ResolveNormalAtContact(
                manifold,
                manifold.GetContactPoint(1),
                ref constraint.SecondContact);
        }
        else if (manifold.ContactPointCount == 1)
        {
            ResolveNormalAtContact(
                manifold,
                manifold.GetContactPoint(0),
                ref constraint.FirstContact);
        }

        ResolveFrictionAtContact(
            manifold,
            manifold.GetContactPoint(0),
            ref constraint.FirstContact);
        if (manifold.ContactPointCount >= 2)
        {
            ResolveFrictionAtContact(
                manifold,
                manifold.GetContactPoint(1),
                ref constraint.SecondContact);
        }
    }

    private static bool TryResolveNormalBlock(VelocityConstraint constraint)
    {
        var manifold = constraint.Manifold;
        var firstBody = manifold.First.Body;
        var secondBody = manifold.Second.Body;
        var firstPoint = manifold.GetContactPoint(0);
        var secondPoint = manifold.GetContactPoint(1);
        var firstRadiusA = firstPoint - firstBody.Owner.Position;
        var firstRadiusB = firstPoint - secondBody.Owner.Position;
        var secondRadiusA = secondPoint - firstBody.Owner.Position;
        var secondRadiusB = secondPoint - secondBody.Owner.Position;
        var firstInverseMass = GetSolverInverseMass(firstBody);
        var secondInverseMass = GetSolverInverseMass(secondBody);
        var inverseMass = firstInverseMass + secondInverseMass;
        var firstLeverA = Cross(firstRadiusA, manifold.Normal);
        var firstLeverB = Cross(firstRadiusB, manifold.Normal);
        var secondLeverA = Cross(secondRadiusA, manifold.Normal);
        var secondLeverB = Cross(secondRadiusB, manifold.Normal);
        var firstInverseInertia = GetSolverInverseMomentOfInertia(firstBody);
        var secondInverseInertia = GetSolverInverseMomentOfInertia(secondBody);
        var k11 = inverseMass +
                  firstLeverA * firstLeverA * firstInverseInertia +
                  firstLeverB * firstLeverB * secondInverseInertia;
        var k22 = inverseMass +
                  secondLeverA * secondLeverA * firstInverseInertia +
                  secondLeverB * secondLeverB * secondInverseInertia;
        var k12 = inverseMass +
                  firstLeverA * secondLeverA * firstInverseInertia +
                  firstLeverB * secondLeverB * secondInverseInertia;
        var determinant = k11 * k22 - k12 * k12;
        if (k11 <= 0.0 || k22 <= 0.0 || determinant <= 1e-12) return false;

        var firstVelocity = secondBody.GetVelocityAtPoint(firstPoint) - firstBody.GetVelocityAtPoint(firstPoint);
        var secondVelocity = secondBody.GetVelocityAtPoint(secondPoint) - firstBody.GetVelocityAtPoint(secondPoint);
        var b1 = Dot(firstVelocity, manifold.Normal) - constraint.FirstContact.RestitutionVelocity;
        var b2 = Dot(secondVelocity, manifold.Normal) - constraint.SecondContact.RestitutionVelocity;
        var a1 = constraint.FirstContact.AccumulatedNormalImpulse;
        var a2 = constraint.SecondContact.AccumulatedNormalImpulse;

        var inverseDeterminant = 1.0 / determinant;
        var delta1 = (-k22 * b1 + k12 * b2) * inverseDeterminant;
        var delta2 = (k12 * b1 - k11 * b2) * inverseDeterminant;
        var x1 = a1 + delta1;
        var x2 = a2 + delta2;
        if (x1 >= 0.0 && x2 >= 0.0)
            return ApplyBlockNormalImpulses(constraint, firstPoint, secondPoint, x1, x2);

        x1 = 0.0;
        delta1 = -a1;
        x2 = a2 - (b2 + k12 * delta1) / k22;
        delta2 = x2 - a2;
        var firstResultVelocity = b1 + k11 * delta1 + k12 * delta2;
        if (x2 >= 0.0 && firstResultVelocity >= -1e-10)
            return ApplyBlockNormalImpulses(constraint, firstPoint, secondPoint, x1, x2);

        x2 = 0.0;
        delta2 = -a2;
        x1 = a1 - (b1 + k12 * delta2) / k11;
        delta1 = x1 - a1;
        var secondResultVelocity = b2 + k12 * delta1 + k22 * delta2;
        if (x1 >= 0.0 && secondResultVelocity >= -1e-10)
            return ApplyBlockNormalImpulses(constraint, firstPoint, secondPoint, x1, x2);

        x1 = 0.0;
        x2 = 0.0;
        delta1 = -a1;
        delta2 = -a2;
        firstResultVelocity = b1 + k11 * delta1 + k12 * delta2;
        secondResultVelocity = b2 + k12 * delta1 + k22 * delta2;
        if (firstResultVelocity < -1e-10 || secondResultVelocity < -1e-10) return false;

        return ApplyBlockNormalImpulses(constraint, firstPoint, secondPoint, x1, x2);
    }

    private static bool ApplyBlockNormalImpulses(
        VelocityConstraint constraint,
        Vector2D<double> firstPoint,
        Vector2D<double> secondPoint,
        double firstAccumulatedImpulse,
        double secondAccumulatedImpulse)
    {
        var manifold = constraint.Manifold;
        var firstIncrement = firstAccumulatedImpulse - constraint.FirstContact.AccumulatedNormalImpulse;
        var secondIncrement = secondAccumulatedImpulse - constraint.SecondContact.AccumulatedNormalImpulse;
        constraint.FirstContact.AccumulatedNormalImpulse = firstAccumulatedImpulse;
        constraint.SecondContact.AccumulatedNormalImpulse = secondAccumulatedImpulse;

        var firstImpulse = manifold.Normal * firstIncrement;
        manifold.First.Body.ApplyCollisionImpulseAtPoint(-firstImpulse, firstPoint);
        manifold.Second.Body.ApplyCollisionImpulseAtPoint(firstImpulse, firstPoint);
        var secondImpulse = manifold.Normal * secondIncrement;
        manifold.First.Body.ApplyCollisionImpulseAtPoint(-secondImpulse, secondPoint);
        manifold.Second.Body.ApplyCollisionImpulseAtPoint(secondImpulse, secondPoint);
        return true;
    }

    private static void ResolveNormalAtContact(
        CollisionManifold manifold,
        Vector2D<double> contactPoint,
        ref ContactImpulseState impulseState)
    {
        var firstBody = manifold.First.Body;
        var secondBody = manifold.Second.Body;
        var firstRadius = contactPoint - firstBody.Owner.Position;
        var secondRadius = contactPoint - secondBody.Owner.Position;
        var firstInverseMass = GetSolverInverseMass(firstBody);
        var secondInverseMass = GetSolverInverseMass(secondBody);
        var firstNormalLever = Cross(firstRadius, manifold.Normal);
        var secondNormalLever = Cross(secondRadius, manifold.Normal);
        var normalDenominator = firstInverseMass + secondInverseMass +
                                firstNormalLever * firstNormalLever * GetSolverInverseMomentOfInertia(firstBody) +
                                secondNormalLever * secondNormalLever * GetSolverInverseMomentOfInertia(secondBody);
        if (normalDenominator <= 0.0) return;

        var relativeVelocity = secondBody.GetVelocityAtPoint(contactPoint) -
                               firstBody.GetVelocityAtPoint(contactPoint);
        var velocityAlongNormal = Dot(relativeVelocity, manifold.Normal);
        var normalImpulseIncrement =
            -(velocityAlongNormal - impulseState.RestitutionVelocity) / normalDenominator;
        var previousNormalImpulse = impulseState.AccumulatedNormalImpulse;
        impulseState.AccumulatedNormalImpulse = Math.Max(
            previousNormalImpulse + normalImpulseIncrement,
            0.0);
        var normalImpulseMagnitude = impulseState.AccumulatedNormalImpulse - previousNormalImpulse;
        var normalImpulse = manifold.Normal * normalImpulseMagnitude;
        firstBody.ApplyCollisionImpulseAtPoint(-normalImpulse, contactPoint);
        secondBody.ApplyCollisionImpulseAtPoint(normalImpulse, contactPoint);
    }

    private static void ResolveFrictionAtContact(
        CollisionManifold manifold,
        Vector2D<double> contactPoint,
        ref ContactImpulseState impulseState)
    {
        var firstBody = manifold.First.Body;
        var secondBody = manifold.Second.Body;
        var firstRadius = contactPoint - firstBody.Owner.Position;
        var secondRadius = contactPoint - secondBody.Owner.Position;
        var firstInverseMass = GetSolverInverseMass(firstBody);
        var secondInverseMass = GetSolverInverseMass(secondBody);
        var relativeVelocity = secondBody.GetVelocityAtPoint(contactPoint) -
                               firstBody.GetVelocityAtPoint(contactPoint);
        var tangent = new Vector2D<double>(-manifold.Normal.Y, manifold.Normal.X);
        var firstTangentLever = Cross(firstRadius, tangent);
        var secondTangentLever = Cross(secondRadius, tangent);
        var tangentDenominator = firstInverseMass + secondInverseMass +
                                 firstTangentLever * firstTangentLever * GetSolverInverseMomentOfInertia(firstBody) +
                                 secondTangentLever * secondTangentLever * GetSolverInverseMomentOfInertia(secondBody);
        if (tangentDenominator <= 0.0) return;

        var tangentImpulseIncrement = -Dot(relativeVelocity, tangent) / tangentDenominator;
        var combinedFriction = Math.Sqrt(firstBody.Friction * secondBody.Friction);
        var maximumFrictionImpulse = impulseState.AccumulatedNormalImpulse * combinedFriction;
        var previousTangentImpulse = impulseState.AccumulatedTangentImpulse;
        impulseState.AccumulatedTangentImpulse = Math.Clamp(
            previousTangentImpulse + tangentImpulseIncrement,
            -maximumFrictionImpulse,
            maximumFrictionImpulse);
        var tangentImpulseMagnitude = impulseState.AccumulatedTangentImpulse - previousTangentImpulse;
        var frictionImpulse = tangent * tangentImpulseMagnitude;
        firstBody.ApplyCollisionImpulseAtPoint(-frictionImpulse, contactPoint);
        secondBody.ApplyCollisionImpulseAtPoint(frictionImpulse, contactPoint);
    }

    private static int ValidateSolverIterationCount(int value, string parameterName)
    {
        if (value is < MinimumSolverIterationCount or > MaximumSolverIterationCount)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                $"Solver iteration counts must be between {MinimumSolverIterationCount} and {MaximumSolverIterationCount}.");
        }

        return value;
    }

    private static double GetSolverInverseMass(RigidBody2D body) =>
        body.IsSleeping ? 0.0 : body.InverseMass;

    private static double GetSolverInverseMomentOfInertia(RigidBody2D body) =>
        body.IsSleeping ? 0.0 : body.InverseMomentOfInertia;

    private static double Dot(Vector2D<double> first, Vector2D<double> second)
    {
        return first.X * second.X + first.Y * second.Y;
    }

    private static double Cross(Vector2D<double> first, Vector2D<double> second) =>
        first.X * second.Y - first.Y * second.X;

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
        return new CollisionContact2D(
            manifold.Second,
            manifold.Normal,
            manifold.Penetration,
            manifold.GetContactPoint(0));
    }

    private static CollisionContact2D CreateContactForSecond(CollisionManifold manifold)
    {
        return new CollisionContact2D(
            manifold.First,
            -manifold.Normal,
            manifold.Penetration,
            manifold.GetContactPoint(0));
    }

    private readonly record struct ColliderPair(long FirstId, long SecondId)
    {
        internal bool Contains(long colliderId) => FirstId == colliderId || SecondId == colliderId;
    }

    private readonly record struct CollisionManifold(
        Collider2D First,
        Collider2D Second,
        Vector2D<double> Normal,
        double Penetration,
        int ContactPointCount,
        Vector2D<double> FirstContactPoint,
        Vector2D<double> SecondContactPoint)
    {
        internal Vector2D<double> GetContactPoint(int index)
        {
            return index switch
            {
                0 when ContactPointCount >= 1 => FirstContactPoint,
                1 when ContactPointCount >= 2 => SecondContactPoint,
                _ => throw new ArgumentOutOfRangeException(nameof(index))
            };
        }
    }

    private sealed class VelocityConstraint(CollisionManifold manifold)
    {
        internal CollisionManifold Manifold { get; } = manifold;
        internal ContactImpulseState FirstContact;
        internal ContactImpulseState SecondContact;
    }

    private struct ContactImpulseState
    {
        internal double AccumulatedNormalImpulse;
        internal double AccumulatedTangentImpulse;
        internal double RestitutionVelocity;
    }

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
