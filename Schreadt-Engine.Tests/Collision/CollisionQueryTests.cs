using Schreadt_Engine.Collision;
using Schreadt_Engine.Component;
using Schreadt_Engine.Component.Logic;
using Schreadt_Engine.Component.PreFab;
using Schreadt_Engine.Core;
using Silk.NET.Maths;

namespace Schreadt_Engine.Tests.Collision;

public sealed class CollisionQueryTests
{
    [Fact]
    public void OverlapPoint_AppliesLayerTriggerAndPredicateFilters()
    {
        var scene = CreateScene();
        var circle = AddCircle(scene, Vector2D<double>.Zero, 1.0, layer: 2);
        var trigger = AddBox(scene, Vector2D<double>.Zero, Vector2D<double>.One, layer: 3, isTrigger: true);

        var all = scene.Collisions.OverlapPoint(Vector2D<double>.Zero);
        var filtered = scene.Collisions.OverlapPoint(
            Vector2D<double>.Zero,
            new CollisionQueryFilter2D(
                CollisionLayerMask2D.FromLayers(2, 3),
                includeTriggers: false,
                collider => ReferenceEquals(collider, circle)));

        Assert.Contains(circle, all);
        Assert.Contains(trigger, all);
        Assert.Equal([circle], filtered);
    }

    [Fact]
    public void OverlapQueries_HandleCircleAndBoxGeometry()
    {
        var scene = CreateScene();
        var circle = AddCircle(scene, new Vector2D<double>(1.4, 1.4), 0.5);
        var box = AddBox(scene, Vector2D<double>.Zero, new Vector2D<double>(2.0, 2.0));

        var circleQuery = scene.Collisions.OverlapCircle(new Vector2D<double>(1.2, 1.2), 0.3);
        var boxQuery = scene.Collisions.OverlapBox(
            new Vector2D<double>(1.2, 1.2),
            new Vector2D<double>(0.5, 0.5));

        Assert.Contains(circle, circleQuery);
        Assert.Contains(box, circleQuery);
        Assert.Contains(circle, boxQuery);
        Assert.Contains(box, boxQuery);
    }

    [Fact]
    public void Queries_IgnoreDisabledAndInactiveColliders()
    {
        var scene = CreateScene();
        var disabled = AddCircle(scene, Vector2D<double>.Zero, 1.0);
        var inactive = AddBox(scene, Vector2D<double>.Zero, Vector2D<double>.One);
        disabled.Enabled = false;
        inactive.Owner.Active = false;

        Assert.Empty(scene.Collisions.OverlapPoint(Vector2D<double>.Zero));
    }

    [Fact]
    public void ReusableOverlapResultCollection_IsClearedBeforeUse()
    {
        var scene = CreateScene();
        var circle = AddCircle(scene, Vector2D<double>.Zero, 1.0);
        ICollection<Collider2D> results = new List<Collider2D> { AddCircle(scene, new Vector2D<double>(5.0), 0.5) };

        var count = scene.Collisions.OverlapPoint(Vector2D<double>.Zero, results);

        Assert.Equal(1, count);
        Assert.Equal([circle], results);
    }

    [Fact]
    public void Raycast_ReturnsNearestHitWithPointNormalAndDistance()
    {
        var scene = CreateScene();
        var nearBox = AddBox(scene, new Vector2D<double>(2.0, 0.0), Vector2D<double>.One);
        AddCircle(scene, new Vector2D<double>(4.0, 0.0), 0.5);

        var found = scene.Collisions.Raycast(
            Vector2D<double>.Zero,
            new Vector2D<double>(2.0, 0.0),
            10.0,
            out var hit);

        Assert.True(found);
        Assert.Same(nearBox, hit.Collider);
        Assert.Equal(new Vector2D<double>(1.5, 0.0), hit.Point);
        Assert.Equal(new Vector2D<double>(-1.0, 0.0), hit.Normal);
        Assert.Equal(1.5, hit.Distance, 10);
        Assert.Equal(0.15, hit.Fraction, 10);
    }

    [Fact]
    public void RaycastAll_IsDistanceSortedAndCanExcludeTriggers()
    {
        var scene = CreateScene();
        var farCircle = AddCircle(scene, new Vector2D<double>(4.0, 0.0), 0.5);
        var trigger = AddBox(
            scene,
            new Vector2D<double>(2.0, 0.0),
            Vector2D<double>.One,
            isTrigger: true);
        var results = new List<RaycastHit2D>();

        scene.Collisions.RaycastAll(Vector2D<double>.Zero, Vector2D<double>.UnitX, 10.0, results);
        Assert.Equal([trigger, farCircle], results.Select(hit => hit.Collider));

        scene.Collisions.RaycastAll(
            Vector2D<double>.Zero,
            Vector2D<double>.UnitX,
            10.0,
            results,
            new CollisionQueryFilter2D(CollisionLayerMask2D.All, includeTriggers: false));
        Assert.Equal([farCircle], results.Select(hit => hit.Collider));
    }

    [Fact]
    public void Raycast_FromInsideColliderReturnsImmediateHit()
    {
        var scene = CreateScene();
        var circle = AddCircle(scene, Vector2D<double>.Zero, 1.0);

        Assert.True(scene.Collisions.Raycast(
            Vector2D<double>.Zero,
            Vector2D<double>.UnitY,
            0.0,
            out var hit));
        Assert.Same(circle, hit.Collider);
        Assert.Equal(0.0, hit.Distance);
        Assert.Equal(-Vector2D<double>.UnitY, hit.Normal);
    }

    [Fact]
    public void Statistics_ReportCurrentWorldAndLastStepActivity()
    {
        var scene = CreateScene();
        AddCircle(scene, Vector2D<double>.Zero, 1.0);
        AddBox(scene, Vector2D<double>.Zero, Vector2D<double>.One);

        scene.Collisions.Step(0.0);
        var statistics = scene.Collisions.Statistics;

        Assert.Equal(2, statistics.RegisteredColliderCount);
        Assert.Equal(2, statistics.ActiveColliderCount);
        Assert.Equal(2, statistics.RigidBodyCount);
        Assert.Equal(1, statistics.PairCheckCount);
        Assert.Equal(1, statistics.NarrowPhaseTestCount);
        Assert.Equal(1, statistics.ContactCount);
    }

    [Fact]
    public void DebugDraw_UsesColliderGeometryAndConfiguredStateColors()
    {
        var scene = CreateScene();
        var circle = AddCircle(scene, new Vector2D<double>(1.0, 2.0), 0.75);
        circle.BodyType = CollisionBodyType2D.Dynamic;
        var trigger = AddBox(scene, new Vector2D<double>(-1.0, 0.0), new Vector2D<double>(2.0, 0.5), isTrigger: true);
        var hidden = AddCircle(scene, new Vector2D<double>(5.0), 1.0);
        hidden.Enabled = false;
        var renderer = new RecordingRenderContext();
        scene.Collisions.DebugDraw.Enabled = true;

        scene.Collisions.DrawDiagnostics(renderer);

        var circleDraw = Assert.Single(renderer.Circles);
        Assert.Equal(circle.Center, circleDraw.Center);
        Assert.Equal(circle.Radius, circleDraw.Radius);
        Assert.Equal(scene.Collisions.DebugDraw.DynamicColor, circleDraw.Color);
        var boxDraw = Assert.Single(renderer.Rectangles);
        Assert.Equal(trigger.Center, boxDraw.Center);
        Assert.Equal(trigger.Size, boxDraw.Size);
        Assert.Equal(scene.Collisions.DebugDraw.TriggerColor, boxDraw.Color);
    }

    [Fact]
    public void QueryMethods_ValidateGeometry()
    {
        var world = CreateScene().Collisions;

        Assert.Throws<ArgumentOutOfRangeException>(() => world.OverlapPoint(new Vector2D<double>(double.NaN, 0.0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => world.OverlapCircle(Vector2D<double>.Zero, -1.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => world.OverlapBox(Vector2D<double>.Zero, Vector2D<double>.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => world.Raycast(
            Vector2D<double>.Zero,
            Vector2D<double>.Zero,
            1.0,
            out _));
    }

    private static Scene CreateScene() => new("queries", new EmptySceneLogic());

    private static CircleCollider2D AddCircle(
        Scene scene,
        Vector2D<double> position,
        double radius,
        int layer = 0)
    {
        var gameObject = new Circle { Position = position, Radius = radius };
        var collider = gameObject.AddComponent(new CircleCollider2D(radius) { CollisionLayer = layer });
        scene.AddChild(gameObject);
        return collider;
    }

    private static AxisAlignedBoxCollider2D AddBox(
        Scene scene,
        Vector2D<double> position,
        Vector2D<double> size,
        int layer = 0,
        bool isTrigger = false)
    {
        var gameObject = new Rectangle2D { Position = position, Size = size };
        var collider = gameObject.AddComponent(new AxisAlignedBoxCollider2D(size)
        {
            CollisionLayer = layer,
            IsTrigger = isTrigger
        });
        scene.AddChild(gameObject);
        return collider;
    }

    private sealed class EmptySceneLogic : SceneLogic
    {
        public override void Init()
        {
        }

        public override void Update(double dt)
        {
        }
    }

    private sealed class RecordingRenderContext : IRenderContext2D
    {
        internal List<(Vector2D<double> Center, double Radius, Vector4D<float> Color)> Circles { get; } = [];
        internal List<(Vector2D<double> Center, Vector2D<double> Size, Vector4D<float> Color)> Rectangles { get; } = [];

        public Vector2D<int> ViewportSize => new(1280, 720);

        public void DrawCircle(Vector2D<double> center, double radius, Vector4D<float> color)
            => Circles.Add((center, radius, color));

        public void DrawRectangle(
            Vector2D<double> center,
            Vector2D<double> size,
            Vector4D<float> color,
            double rotationRadians = 0.0)
            => Rectangles.Add((center, size, color));

        public void DrawPolygon(
            Vector2D<double> center,
            IReadOnlyList<Vector2D<double>> localVertices,
            Vector2D<double> scale,
            double rotationRadians,
            Vector4D<float> color)
        {
        }

        public void DrawSprite(
            string imageAssetId,
            Vector2D<double> center,
            Vector2D<double> size,
            Vector4D<float> tint,
            double rotationRadians = 0.0,
            TextureRegion? region = null,
            TextureSampling sampling = TextureSampling.Linear)
        {
        }

        public void DrawText(
            string text,
            Vector2D<float> position,
            float scale,
            Vector4D<float> color,
            Vector4D<float> backgroundColor,
            float padding = 0.0f)
        {
        }

        public void DrawScreenRectangle(
            Vector2D<float> position,
            Vector2D<float> size,
            Vector4D<float> color)
        {
        }
    }
}
