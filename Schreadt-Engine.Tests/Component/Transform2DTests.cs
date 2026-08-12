using Schreadt_Engine.Collision;
using Schreadt_Engine.Component;
using Schreadt_Engine.Component.Logic;
using Schreadt_Engine.Component.PreFab;
using Schreadt_Engine.Core;
using Silk.NET.Maths;

namespace Schreadt_Engine.Tests.Component;

public sealed class Transform2DTests
{
    [Fact]
    public void LocalTransform_RejectsInvalidValuesWithoutChangingState()
    {
        var transform = new TestObject().Transform;

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            transform.LocalPosition = new Vector2D<double>(double.NaN, 0.0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            transform.LocalRotation = double.PositiveInfinity);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            transform.LocalScale = new Vector2D<double>(1.0, 0.0));

        Assert.Equal(Vector2D<double>.Zero, transform.LocalPosition);
        Assert.Equal(0.0, transform.LocalRotation);
        Assert.Equal(Vector2D<double>.One, transform.LocalScale);
    }

    [Fact]
    public void WorldTransform_ComposesPositionRotationAndScaleThroughMultipleLevels()
    {
        var root = new TestObject();
        root.Transform.LocalPosition = new Vector2D<double>(2.0, 3.0);
        root.Transform.LocalRotation = Math.PI / 2.0;
        root.Transform.LocalScale = new Vector2D<double>(2.0, 3.0);

        var child = new TestObject();
        child.Transform.LocalPosition = Vector2D<double>.UnitX;
        child.Transform.LocalRotation = Math.PI / 4.0;
        child.Transform.LocalScale = new Vector2D<double>(0.5, 2.0);
        root.AddChild(child);

        var grandchild = new TestObject();
        grandchild.Transform.LocalPosition = Vector2D<double>.UnitY;
        grandchild.Transform.LocalRotation = -Math.PI / 8.0;
        grandchild.Transform.LocalScale = new Vector2D<double>(4.0, 0.5);
        child.AddChild(grandchild);

        AssertVectorEqual(new Vector2D<double>(2.0, 5.0), child.Transform.WorldPosition);
        Assert.Equal(3.0 * Math.PI / 4.0, child.Transform.WorldRotation, 10);
        AssertVectorEqual(new Vector2D<double>(1.0, 6.0), child.Transform.WorldScale);

        var expectedGrandchildPosition = child.Transform.WorldPosition +
                                         Transform2D.Rotate(
                                             new Vector2D<double>(0.0, 6.0),
                                             child.Transform.WorldRotation);
        AssertVectorEqual(expectedGrandchildPosition, grandchild.Transform.WorldPosition);
        Assert.Equal(5.0 * Math.PI / 8.0, grandchild.Transform.WorldRotation, 10);
        AssertVectorEqual(new Vector2D<double>(4.0, 3.0), grandchild.Transform.WorldScale);
    }

    [Fact]
    public void SetParent_KeepWorldTransformPreservesVisibleTransform()
    {
        var firstParent = CreateParent(new(3.0, -2.0), 0.45, new(2.0, 0.75));
        var secondParent = CreateParent(new(-4.0, 1.5), -0.8, new(0.5, 3.0));
        var child = new TestObject();
        child.Transform.LocalPosition = new Vector2D<double>(1.2, -0.4);
        child.Transform.LocalRotation = 0.3;
        child.Transform.LocalScale = new Vector2D<double>(1.5, 0.6);
        firstParent.AddChild(child);
        var worldPosition = child.Transform.WorldPosition;
        var worldRotation = child.Transform.WorldRotation;
        var worldScale = child.Transform.WorldScale;

        child.SetParent(secondParent, keepWorldTransform: true);

        Assert.Same(secondParent, child.Parent);
        AssertVectorEqual(worldPosition, child.Transform.WorldPosition);
        Assert.Equal(worldRotation, child.Transform.WorldRotation, 10);
        AssertVectorEqual(worldScale, child.Transform.WorldScale);
    }

    [Fact]
    public void SetParent_WithoutKeepingWorldTransformPreservesLocalTransform()
    {
        var firstParent = CreateParent(new(3.0, -2.0), 0.45, new(2.0, 0.75));
        var secondParent = CreateParent(new(-4.0, 1.5), -0.8, new(0.5, 3.0));
        var child = new TestObject();
        child.Transform.LocalPosition = new Vector2D<double>(1.2, -0.4);
        child.Transform.LocalRotation = 0.3;
        child.Transform.LocalScale = new Vector2D<double>(1.5, 0.6);
        firstParent.AddChild(child);
        var localPosition = child.Transform.LocalPosition;
        var localRotation = child.Transform.LocalRotation;
        var localScale = child.Transform.LocalScale;
        var previousWorldPosition = child.Transform.WorldPosition;

        child.SetParent(secondParent);

        Assert.Same(secondParent, child.Parent);
        AssertVectorEqual(localPosition, child.Transform.LocalPosition);
        Assert.Equal(localRotation, child.Transform.LocalRotation, 10);
        AssertVectorEqual(localScale, child.Transform.LocalScale);
        Assert.NotEqual(previousWorldPosition, child.Transform.WorldPosition);
    }

    [Fact]
    public void SetParent_StillRejectsHierarchyCycles()
    {
        var root = new TestObject();
        var child = new TestObject();
        var grandchild = new TestObject();
        root.AddChild(child);
        child.AddChild(grandchild);

        Assert.Throws<InvalidOperationException>(() => root.SetParent(grandchild));

        Assert.Null(root.Parent);
        Assert.Same(root, child.Parent);
        Assert.Same(child, grandchild.Parent);
    }

    [Fact]
    public void UpdateTransform_IsVisibleToRenderingInTheSameFrame()
    {
        var parent = new TestObject();
        parent.Transform.LocalPosition = new Vector2D<double>(2.0, 1.0);
        parent.Transform.LocalRotation = Math.PI / 2.0;
        parent.Transform.LocalScale = new Vector2D<double>(2.0, 3.0);
        var child = new UpdatingRectangle
        {
            Size = new Vector2D<double>(0.5, 0.25)
        };
        parent.AddChild(child);
        parent.Init();
        var renderer = new RecordingRenderContext();

        parent.Update(0.0);
        parent.Render(renderer);

        var draw = Assert.Single(renderer.Rectangles);
        AssertVectorEqual(new Vector2D<double>(2.0, 3.0), draw.Center);
        AssertVectorEqual(new Vector2D<double>(1.0, 0.75), draw.Size);
        Assert.Equal(3.0 * Math.PI / 4.0, draw.Rotation, 10);
    }

    [Fact]
    public void ColliderCentersUseWorldPositionAndRotationButIgnoreTransformScale()
    {
        var parent = CreateParent(new(2.0, 3.0), Math.PI / 2.0, new(4.0, 5.0));
        var circle = new Circle();
        circle.Transform.LocalPosition = Vector2D<double>.UnitX;
        circle.Transform.LocalRotation = Math.PI / 2.0;
        var collider = circle.AddComponent(new CircleCollider2D(0.75)
        {
            Offset = Vector2D<double>.UnitX
        });
        parent.AddChild(circle);

        AssertVectorEqual(new Vector2D<double>(1.0, 7.0), collider.Center);
        Assert.Equal(0.75, collider.Radius);
    }

    [Fact]
    public void UpdateTransform_IsVisibleToTheFollowingPhysicsStep()
    {
        var scene = new Scene("transform-physics", new EmptySceneLogic());
        var parent = new TestObject { Position = new Vector2D<double>(2.0, 0.0) };
        var child = new UpdatingCircle();
        var childCollider = child.AddComponent(new CircleCollider2D(0.2));
        var trigger = new Circle { Position = new Vector2D<double>(3.0, 0.0), Radius = 0.3 };
        trigger.AddComponent(new CircleCollider2D(0.3) { IsTrigger = true });
        var entered = 0;
        childCollider.CollisionEntered += _ => entered++;
        parent.AddChild(child);
        scene.AddChild(parent);
        scene.AddChild(trigger);
        scene.Init();

        scene.Update(0.0);
        scene.Collisions.Step(0.0);

        AssertVectorEqual(new Vector2D<double>(3.0, 0.0), childCollider.Center);
        Assert.Equal(1, entered);
    }

    [Fact]
    public void CameraViewUsesInheritedWorldTransform()
    {
        var parent = CreateParent(new(2.0, 3.0), Math.PI / 2.0, new(2.0, 2.0));
        var camera = new Camera();
        camera.Transform.LocalPosition = Vector2D<double>.UnitX;
        camera.Transform.LocalRotation = Math.PI / 4.0;
        parent.AddChild(camera);

        var viewportCenter = camera.WorldToViewportPoint(camera.Transform.WorldPosition, 16.0 / 9.0);

        AssertVectorEqual(new Vector2D<double>(2.0, 5.0), camera.Position);
        Assert.Equal(3.0 * Math.PI / 4.0, camera.RotationRadians, 10);
        AssertVectorEqual(new Vector2D<double>(0.5, 0.5), viewportCenter);
    }

    private static TestObject CreateParent(
        Vector2D<double> position,
        double rotation,
        Vector2D<double> scale)
    {
        var parent = new TestObject();
        parent.Transform.LocalPosition = position;
        parent.Transform.LocalRotation = rotation;
        parent.Transform.LocalScale = scale;
        return parent;
    }

    private static void AssertVectorEqual(Vector2D<double> expected, Vector2D<double> actual)
    {
        Assert.Equal(expected.X, actual.X, 10);
        Assert.Equal(expected.Y, actual.Y, 10);
    }

    private sealed class TestObject : GameObject;

    private sealed class UpdatingRectangle : Rectangle2D
    {
        protected override void OnUpdate(double dt)
        {
            Transform.LocalPosition = Vector2D<double>.UnitX;
            Transform.LocalRotation = Math.PI / 4.0;
        }
    }

    private sealed class UpdatingCircle : Circle
    {
        protected override void OnUpdate(double dt)
        {
            Transform.LocalPosition = Vector2D<double>.UnitX;
        }
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
        internal List<(Vector2D<double> Center, Vector2D<double> Size, double Rotation)> Rectangles { get; } = [];

        public Vector2D<int> ViewportSize => new(1280, 720);

        public void DrawCircle(Vector2D<double> center, double radius, Vector4D<float> color)
        {
        }

        public void DrawRectangle(
            Vector2D<double> center,
            Vector2D<double> size,
            Vector4D<float> color,
            double rotationRadians = 0.0) => Rectangles.Add((center, size, rotationRadians));

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
