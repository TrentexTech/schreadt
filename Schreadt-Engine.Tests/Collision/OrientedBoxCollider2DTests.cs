using Schreadt_Engine.Collision;
using Schreadt_Engine.Component;
using Schreadt_Engine.Component.Logic;
using Schreadt_Engine.Component.PreFab;
using Schreadt_Engine.Core;
using Silk.NET.Maths;

namespace Schreadt_Engine.Tests.Collision;

public sealed class OrientedBoxCollider2DTests
{
    [Fact]
    public void Geometry_UsesOwnerRotationAndLocalRotationButIgnoresTransformScale()
    {
        var owner = new Rectangle2D
        {
            Position = new Vector2D<double>(2.0, 3.0),
            RotationRadians = Math.PI / 2.0,
            Size = new Vector2D<double>(4.0, 2.0),
            Transform = { LocalScale = new Vector2D<double>(5.0, 7.0) }
        };
        var collider = owner.AddComponent(new OrientedBoxCollider2D(owner.Size)
        {
            Offset = Vector2D<double>.UnitX,
            RotationOffset = Math.PI / 4.0
        });

        AssertVectorEqual(new Vector2D<double>(2.0, 4.0), collider.Center);
        Assert.Equal(3.0 * Math.PI / 4.0, collider.WorldRotation, 10);
        AssertVectorEqual(
            new Vector2D<double>(-1.0 / Math.Sqrt(2.0), 1.0 / Math.Sqrt(2.0)),
            collider.AxisX);
        AssertVectorEqual(
            new Vector2D<double>(-1.0 / Math.Sqrt(2.0), -1.0 / Math.Sqrt(2.0)),
            collider.AxisY);
        Assert.Equal(owner.Size, collider.Size);

        var vertices = collider.GetWorldVertices();
        Assert.Equal(4, vertices.Length);
        Assert.All(vertices, vertex =>
        {
            var offset = vertex - collider.Center;
            var localX = Dot(offset, collider.AxisX);
            var localY = Dot(offset, collider.AxisY);
            Assert.Equal(collider.HalfSize.X, Math.Abs(localX), 10);
            Assert.Equal(collider.HalfSize.Y, Math.Abs(localY), 10);
        });
    }

    [Fact]
    public void Geometry_RejectsInvalidValuesWithoutChangingState()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new OrientedBoxCollider2D(Vector2D<double>.Zero));

        var collider = new OrientedBoxCollider2D(new Vector2D<double>(2.0, 1.0))
        {
            Offset = new Vector2D<double>(0.25, -0.5),
            RotationOffset = 0.3
        };

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            collider.Size = new Vector2D<double>(double.NaN, 1.0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            collider.Offset = new Vector2D<double>(0.0, double.PositiveInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            collider.RotationOffset = double.NaN);

        Assert.Equal(new Vector2D<double>(2.0, 1.0), collider.Size);
        Assert.Equal(new Vector2D<double>(0.25, -0.5), collider.Offset);
        Assert.Equal(0.3, collider.RotationOffset);
    }

    [Fact]
    public void OrientedBoxPair_UsesAxisWithLeastPenetration()
    {
        const double rotation = Math.PI / 4.0;
        var first = CreateOrientedBox(new Vector2D<double>(2.0, 1.0), Vector2D<double>.Zero, rotation);
        var expectedNormal = new Vector2D<double>(-Math.Sin(rotation), Math.Cos(rotation));
        var second = CreateOrientedBox(
            new Vector2D<double>(2.0, 1.0),
            expectedNormal * 0.75,
            rotation);

        var collided = new OrientedBoxOrientedBoxNarrowPhase2D()
            .TryCollide(first, second, out var result);

        Assert.True(collided);
        AssertVectorEqual(expectedNormal, result.Normal);
        Assert.Equal(0.25, result.Penetration, 10);
    }

    [Fact]
    public void OrientedBoxPair_ReturnsFalseForSeparationAlongRotatedAxis()
    {
        const double rotation = Math.PI / 4.0;
        var first = CreateOrientedBox(new Vector2D<double>(2.0, 1.0), Vector2D<double>.Zero, rotation);
        var separatingAxis = new Vector2D<double>(-Math.Sin(rotation), Math.Cos(rotation));
        var second = CreateOrientedBox(
            new Vector2D<double>(2.0, 1.0),
            separatingAxis * 1.01,
            rotation);

        Assert.False(new OrientedBoxOrientedBoxNarrowPhase2D()
            .TryCollide(first, second, out _));
    }

    [Theory]
    [InlineData(-0.0000000001, true)]
    [InlineData(0.0, true)]
    [InlineData(0.0000000001, false)]
    public void OrientedBoxPair_UsesStableTouchingBoundary(double separationDelta, bool expectedCollision)
    {
        const double rotation = Math.PI / 7.0;
        var first = CreateOrientedBox(new Vector2D<double>(2.0, 1.0), Vector2D<double>.Zero, rotation);
        var separatingAxis = first.AxisY;
        var second = CreateOrientedBox(
            new Vector2D<double>(2.0, 1.0),
            separatingAxis * (1.0 + separationDelta),
            rotation);

        var collided = new OrientedBoxOrientedBoxNarrowPhase2D()
            .TryCollide(first, second, out _);

        Assert.Equal(expectedCollision, collided);
    }

    [Fact]
    public void CircleOrientedBox_DetectsRotatedCornerOverlap()
    {
        const double rotation = Math.PI / 4.0;
        var box = CreateOrientedBox(new Vector2D<double>(2.0, 2.0), Vector2D<double>.Zero, rotation);
        var localCircleCenter = new Vector2D<double>(1.3, 1.3);
        var circle = CreateCircle(0.5, Transform2D.Rotate(localCircleCenter, rotation));

        var collided = new CircleOrientedBoxNarrowPhase2D().TryCollide(circle, box, out var result);

        Assert.True(collided);
        var expectedLocalNormal = new Vector2D<double>(-1.0 / Math.Sqrt(2.0), -1.0 / Math.Sqrt(2.0));
        AssertVectorEqual(Transform2D.Rotate(expectedLocalNormal, rotation), result.Normal);
        Assert.Equal(0.5 - Math.Sqrt(0.18), result.Penetration, 10);
    }

    [Fact]
    public void CollisionWorld_UsesBuiltInAxisAlignedOrientedPairInEitherOrder()
    {
        var scene = CreateScene();
        var oriented = AddOrientedBox(
            scene,
            new Vector2D<double>(0.8, 0.0),
            Vector2D<double>.One,
            Math.PI / 4.0);
        var axisAligned = AddAxisAlignedBox(scene, Vector2D<double>.Zero, Vector2D<double>.One);
        CollisionContact2D? contact = null;
        oriented.CollisionEntered += value => contact = value;

        scene.Collisions.Step(0.0);

        Assert.True(contact.HasValue);
        Assert.Same(axisAligned, contact.Value.Other);
        AssertVectorEqual(-Vector2D<double>.UnitX, contact.Value.Normal);
        Assert.Equal((1.0 + Math.Sqrt(2.0)) * 0.5 - 0.8, contact.Value.Penetration, 10);
    }

    [Fact]
    public void CollisionWorld_UsesBuiltInCircleOrientedPairWhenBoxWasAddedFirst()
    {
        var scene = CreateScene();
        var box = AddOrientedBox(
            scene,
            Vector2D<double>.Zero,
            new Vector2D<double>(2.0, 1.0),
            Math.PI / 4.0);
        var localCircleCenter = new Vector2D<double>(1.1, 0.0);
        var circle = CreateCircle(0.2, Transform2D.Rotate(localCircleCenter, box.WorldRotation));
        scene.AddChild(circle.Owner);
        CollisionContact2D? contact = null;
        box.CollisionEntered += value => contact = value;

        scene.Collisions.Step(0.0);

        Assert.True(contact.HasValue);
        Assert.Same(circle, contact.Value.Other);
        AssertVectorEqual(box.AxisX, contact.Value.Normal);
        Assert.Equal(0.1, contact.Value.Penetration, 10);
    }

    [Fact]
    public void CollisionWorld_ObservesAncestorRotationBeforeNextStep()
    {
        var scene = CreateScene();
        var parent = new Rectangle2D();
        var child = new Rectangle2D();
        var box = child.AddComponent(new OrientedBoxCollider2D(new Vector2D<double>(2.0, 0.2)));
        var circle = CreateCircle(0.15, new Vector2D<double>(0.0, 0.8));
        parent.AddChild(child);
        scene.AddChild(parent);
        scene.AddChild(circle.Owner);
        var entered = false;
        box.CollisionEntered += _ => entered = true;

        scene.Collisions.Step(0.0);
        Assert.False(entered);

        parent.Transform.LocalRotation = Math.PI / 2.0;
        scene.Collisions.Step(0.0);

        Assert.True(entered);
    }

    [Fact]
    public void OverlapQueries_RecognizeRotatedBoxGeometry()
    {
        const double rotation = Math.PI / 4.0;
        var scene = CreateScene();
        var box = AddOrientedBox(scene, Vector2D<double>.Zero, new Vector2D<double>(2.0, 0.5), rotation);
        var pointInside = Transform2D.Rotate(new Vector2D<double>(0.9, 0.0), rotation);
        var pointOutside = Transform2D.Rotate(new Vector2D<double>(0.0, 0.3), rotation);

        Assert.Contains(box, scene.Collisions.OverlapPoint(pointInside));
        Assert.DoesNotContain(box, scene.Collisions.OverlapPoint(pointOutside));
        Assert.Contains(box, scene.Collisions.OverlapCircle(pointOutside, 0.06));
        Assert.Contains(box, scene.Collisions.OverlapBox(pointInside, new Vector2D<double>(0.1, 0.1)));
        Assert.DoesNotContain(
            box,
            scene.Collisions.OverlapBox(
                Transform2D.Rotate(new Vector2D<double>(0.0, 0.5), rotation),
                new Vector2D<double>(0.1, 0.1)));
    }

    [Fact]
    public void Raycast_ReturnsRotatedBoxPointNormalAndDistance()
    {
        const double rotation = Math.PI / 4.0;
        var scene = CreateScene();
        var box = AddOrientedBox(scene, new Vector2D<double>(2.0, 1.0), new Vector2D<double>(2.0, 1.0), rotation);
        var direction = box.AxisX;
        var origin = box.Center - direction * 3.0;

        var found = scene.Collisions.Raycast(origin, direction, 5.0, out var hit);

        Assert.True(found);
        Assert.Same(box, hit.Collider);
        Assert.Equal(2.0, hit.Distance, 10);
        AssertVectorEqual(box.Center - direction, hit.Point);
        AssertVectorEqual(-direction, hit.Normal);
    }

    [Fact]
    public void QueriesObserveAncestorRotationImmediately()
    {
        var scene = CreateScene();
        var parent = new Rectangle2D();
        var child = new Rectangle2D { Position = new Vector2D<double>(1.0, 0.0) };
        var box = child.AddComponent(new OrientedBoxCollider2D(new Vector2D<double>(2.0, 0.25)));
        parent.AddChild(child);
        scene.AddChild(parent);

        Assert.Contains(box, scene.Collisions.OverlapPoint(new Vector2D<double>(1.8, 0.0)));

        parent.Transform.LocalRotation = Math.PI / 2.0;

        Assert.DoesNotContain(box, scene.Collisions.OverlapPoint(new Vector2D<double>(1.8, 0.0)));
        Assert.Contains(box, scene.Collisions.OverlapPoint(new Vector2D<double>(0.0, 1.8)));
    }

    [Fact]
    public void DebugDraw_UsesPhysicalRotationAndStateColor()
    {
        var scene = CreateScene();
        var box = AddOrientedBox(
            scene,
            new Vector2D<double>(1.0, 2.0),
            new Vector2D<double>(2.0, 0.5),
            Math.PI / 3.0);
        box.IsTrigger = true;
        var renderer = new RecordingRenderContext();
        scene.Collisions.DebugDraw.Enabled = true;

        scene.Collisions.DrawDiagnostics(renderer);

        var draw = Assert.Single(renderer.Rectangles);
        Assert.Equal(box.Center, draw.Center);
        Assert.Equal(box.Size, draw.Size);
        Assert.Equal(box.WorldRotation, draw.Rotation, 10);
        Assert.Equal(scene.Collisions.DebugDraw.TriggerColor, draw.Color);
    }

    private static Scene CreateScene() => new("oriented-boxes", new EmptySceneLogic());

    private static OrientedBoxCollider2D AddOrientedBox(
        Scene scene,
        Vector2D<double> position,
        Vector2D<double> size,
        double rotation)
    {
        var collider = CreateOrientedBox(size, position, rotation);
        scene.AddChild(collider.Owner);
        return collider;
    }

    private static OrientedBoxCollider2D CreateOrientedBox(
        Vector2D<double> size,
        Vector2D<double> position,
        double rotation)
    {
        var rectangle = new Rectangle2D { Position = position, Size = size, RotationRadians = rotation };
        return rectangle.AddComponent(new OrientedBoxCollider2D(size));
    }

    private static AxisAlignedBoxCollider2D AddAxisAlignedBox(
        Scene scene,
        Vector2D<double> position,
        Vector2D<double> size)
    {
        var rectangle = new Rectangle2D { Position = position, Size = size };
        var collider = rectangle.AddComponent(new AxisAlignedBoxCollider2D(size));
        scene.AddChild(rectangle);
        return collider;
    }

    private static CircleCollider2D CreateCircle(double radius, Vector2D<double> position)
    {
        var circle = new Circle { Position = position, Radius = radius };
        return circle.AddComponent(new CircleCollider2D(radius));
    }

    private static double Dot(Vector2D<double> first, Vector2D<double> second) =>
        first.X * second.X + first.Y * second.Y;

    private static void AssertVectorEqual(Vector2D<double> expected, Vector2D<double> actual)
    {
        Assert.Equal(expected.X, actual.X, 10);
        Assert.Equal(expected.Y, actual.Y, 10);
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
        internal List<(Vector2D<double> Center, Vector2D<double> Size, Vector4D<float> Color, double Rotation)> Rectangles
        { get; } = [];

        public Vector2D<int> ViewportSize => new(1280, 720);

        public void DrawCircle(Vector2D<double> center, double radius, Vector4D<float> color)
        {
        }

        public void DrawRectangle(
            Vector2D<double> center,
            Vector2D<double> size,
            Vector4D<float> color,
            double rotationRadians = 0.0) =>
            Rectangles.Add((center, size, color, rotationRadians));

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
