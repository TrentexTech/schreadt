using Schreadt_Engine.Collision;
using Schreadt_Engine.Component;
using Schreadt_Engine.Component.Logic;
using Schreadt_Engine.Component.PreFab;
using Schreadt_Engine.Core;
using Silk.NET.Maths;

namespace Schreadt_Engine.Tests.Collision;

public sealed class RevoluteJoint2DTests
{
    [Fact]
    public void WorldAnchor_SuspendedBodySwingsWithoutAnchorDrift()
    {
        var scene = CreateScene(new Vector2D<double>(0.0, -9.81));
        var body = AddBody(scene, new Vector2D<double>(1.0, 0.0));
        var joint = body.Owner.AddComponent(new RevoluteJoint2D(Vector2D<double>.Zero));

        for (var step = 0; step < 720; step++) scene.Collisions.Step(1.0 / 120.0);

        Assert.True(body.Owner.Position.Y < -0.5);
        Assert.InRange(Length(joint.SecondWorldAnchor - joint.FirstWorldAnchor), 0.0, 0.012);
        Assert.Equal(1, scene.Collisions.Statistics.RegisteredJointCount);
        Assert.Equal(1, scene.Collisions.Statistics.ActiveJointCount);
    }

    [Fact]
    public void PointImpulse_OnEitherSideRotatesSeesawInOppositeDirections()
    {
        var leftRotation = RunSeesawImpulse(-1.0);
        var rightRotation = RunSeesawImpulse(1.0);

        Assert.True(leftRotation > 0.05, $"Expected a positive rotation, got {leftRotation}.");
        Assert.True(rightRotation < -0.05, $"Expected a negative rotation, got {rightRotation}.");
    }

    [Fact]
    public void BodyToBodyJoint_KeepsBothLocalAnchorsTogether()
    {
        var scene = CreateScene(Vector2D<double>.Zero);
        var first = AddBody(scene, new Vector2D<double>(-1.0, 0.0));
        var second = AddBody(scene, new Vector2D<double>(1.0, 0.0));
        var joint = first.Owner.AddComponent(new RevoluteJoint2D(
            second,
            new Vector2D<double>(1.0, 0.0),
            new Vector2D<double>(-1.0, 0.0)));
        first.Velocity = new Vector2D<double>(0.0, 2.0);
        second.Velocity = new Vector2D<double>(0.0, -2.0);

        for (var step = 0; step < 360; step++) scene.Collisions.Step(1.0 / 120.0);

        Assert.InRange(Length(joint.SecondWorldAnchor - joint.FirstWorldAnchor), 0.0, 0.012);
        Assert.NotEqual(0.0, first.Owner.Transform.WorldRotation);
        Assert.NotEqual(0.0, second.Owner.Transform.WorldRotation);
    }

    [Fact]
    public void AngleLimits_StopOutwardMotionAtBothBounds()
    {
        var lower = RunTowardLimit(initialAngularVelocity: -8.0);
        var upper = RunTowardLimit(initialAngularVelocity: 8.0);

        Assert.InRange(lower, -0.22, -0.16);
        Assert.InRange(upper, 0.16, 0.22);
    }

    [Fact]
    public void BreakImpulse_RemovesJointAndRaisesEventOnce()
    {
        var scene = CreateScene(Vector2D<double>.Zero);
        var body = AddBody(scene, new Vector2D<double>(1.0, 0.0));
        var joint = body.Owner.AddComponent(new RevoluteJoint2D(Vector2D<double>.Zero)
        {
            BreakImpulseThreshold = 0.01
        });
        var broken = 0;
        joint.Broken += _ => broken++;
        body.Velocity = new Vector2D<double>(0.0, 5.0);

        scene.Collisions.Step(1.0 / 120.0);
        scene.Collisions.Step(1.0 / 120.0);

        Assert.True(joint.IsBroken);
        Assert.Equal(1, broken);
        Assert.Equal(0, scene.Collisions.Statistics.RegisteredJointCount);
        Assert.Equal(0, scene.Collisions.Statistics.ActiveJointCount);
        Assert.Throws<InvalidOperationException>(() => body.Owner.RemoveComponent(body));

        joint.BreakImpulseThreshold = double.PositiveInfinity;
        joint.Repair();

        Assert.False(joint.IsBroken);
        Assert.Equal(1, scene.Collisions.Statistics.RegisteredJointCount);
    }

    [Fact]
    public void BodyToBodyJoint_RejectsDifferentWorldWithoutPartialRegistration()
    {
        var firstScene = CreateScene(Vector2D<double>.Zero);
        var secondScene = CreateScene(Vector2D<double>.Zero);
        var first = AddBody(firstScene, Vector2D<double>.Zero);
        var second = AddBody(secondScene, Vector2D<double>.Zero);
        var joint = new RevoluteJoint2D(second, Vector2D<double>.Zero, Vector2D<double>.Zero);

        Assert.Throws<InvalidOperationException>(() => first.Owner.AddComponent(joint));

        Assert.False(joint.Attached);
        Assert.Empty(firstScene.Collisions.Joints);
        Assert.Empty(secondScene.Collisions.Joints);
        Assert.Same(first, first.Owner.GetComponent<RigidBody2D>());
        Assert.Same(second, second.Owner.GetComponent<RigidBody2D>());
    }

    [Fact]
    public void RemovingEitherConnectedOwner_RemovesJointImmediately()
    {
        var scene = CreateScene(Vector2D<double>.Zero);
        var first = AddBody(scene, new Vector2D<double>(-0.5, 0.0));
        var second = AddBody(scene, new Vector2D<double>(0.5, 0.0));
        var joint = first.Owner.AddComponent(new RevoluteJoint2D(
            second,
            new Vector2D<double>(0.5, 0.0),
            new Vector2D<double>(-0.5, 0.0)));

        Assert.Equal(1, scene.Collisions.Statistics.RegisteredJointCount);

        Assert.True(scene.RemoveChild(second.Owner));

        Assert.Equal(0, scene.Collisions.Statistics.RegisteredJointCount);
        Assert.Null(joint.World);
    }

    [Fact]
    public void InactiveHierarchy_SuppressesJointUntilReactivated()
    {
        var scene = CreateScene(Vector2D<double>.Zero);
        var parent = new TestObject();
        var bodyOwner = new Rectangle2D { Position = new Vector2D<double>(1.0, 0.0) };
        var body = bodyOwner.AddComponent(new RigidBody2D
        {
            BodyType = CollisionBodyType2D.Dynamic,
            UseGravity = false,
            AllowSleep = false,
            Velocity = Vector2D<double>.UnitY
        });
        parent.AddChild(bodyOwner);
        scene.AddChild(parent);
        var joint = bodyOwner.AddComponent(new RevoluteJoint2D(Vector2D<double>.Zero));
        parent.Active = false;

        scene.Collisions.Step(1.0);

        Assert.Equal(new Vector2D<double>(1.0, 0.0), body.Owner.Position);
        Assert.Equal(0, scene.Collisions.Statistics.ActiveJointCount);
        Assert.Equal(1, scene.Collisions.Statistics.RegisteredJointCount);

        parent.Active = true;
        scene.Collisions.Step(1.0 / 120.0);

        Assert.Equal(1, scene.Collisions.Statistics.ActiveJointCount);
        Assert.InRange(Length(joint.SecondWorldAnchor - joint.FirstWorldAnchor), 0.0, 0.012);
    }

    [Fact]
    public void SceneUnload_ClearsJointAndBodyOwnership()
    {
        var scene = CreateScene(Vector2D<double>.Zero);
        var body = AddBody(scene, new Vector2D<double>(1.0, 0.0));
        var joint = body.Owner.AddComponent(new RevoluteJoint2D(Vector2D<double>.Zero));

        scene.Unload();

        Assert.Empty(scene.Collisions.Joints);
        Assert.Empty(scene.Collisions.Bodies);
        Assert.Null(joint.World);
        Assert.Null(body.World);
    }

    [Fact]
    public void InvalidConfiguration_DoesNotPartiallyChangeState()
    {
        var configuredBeforeAttachment = new RevoluteJoint2D(Vector2D<double>.Zero);
        configuredBeforeAttachment.SetLimits(-0.1, 0.1);

        var scene = CreateScene(Vector2D<double>.Zero);
        var body = AddBody(scene, new Vector2D<double>(1.0, 0.0));
        var joint = body.Owner.AddComponent(new RevoluteJoint2D(Vector2D<double>.Zero));
        joint.SetLimits(-0.2, 0.3);
        joint.BreakImpulseThreshold = 4.0;

        Assert.Throws<ArgumentOutOfRangeException>(() => joint.SetLimits(-0.4, double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => joint.SetLimits(0.5, 0.4));
        Assert.Throws<ArgumentOutOfRangeException>(() => joint.BreakImpulseThreshold = 0.0);

        Assert.True(joint.LimitsEnabled);
        Assert.True(configuredBeforeAttachment.LimitsEnabled);
        Assert.Equal(-0.2, joint.LowerAngle);
        Assert.Equal(0.3, joint.UpperAngle);
        Assert.Equal(4.0, joint.BreakImpulseThreshold);
    }

    [Fact]
    public void Diagnostics_DrawBothAnchorsWhenEnabled()
    {
        var scene = CreateScene(Vector2D<double>.Zero);
        var body = AddBody(scene, new Vector2D<double>(1.0, 0.0));
        body.Owner.AddComponent(new RevoluteJoint2D(Vector2D<double>.Zero));
        scene.Collisions.DebugDraw.Enabled = true;
        var renderer = new RecordingRenderContext();

        scene.Collisions.DrawDiagnostics(renderer);

        Assert.Equal(2, renderer.CircleCount);
        Assert.Equal(1, renderer.RectangleCount);
    }

    private static double RunSeesawImpulse(double x)
    {
        var scene = CreateScene(Vector2D<double>.Zero);
        var body = AddBody(scene, Vector2D<double>.Zero, inertia: 1.0);
        body.Owner.AddComponent(new RevoluteJoint2D(Vector2D<double>.Zero, Vector2D<double>.Zero));
        body.AddImpulseAtPoint(new Vector2D<double>(0.0, -1.0), new Vector2D<double>(x, 0.0));

        for (var step = 0; step < 12; step++) scene.Collisions.Step(1.0 / 120.0);
        return body.Owner.Transform.WorldRotation;
    }

    private static double RunTowardLimit(double initialAngularVelocity)
    {
        var scene = CreateScene(Vector2D<double>.Zero);
        var body = AddBody(scene, Vector2D<double>.Zero, inertia: 1.0);
        var joint = body.Owner.AddComponent(new RevoluteJoint2D(Vector2D<double>.Zero, Vector2D<double>.Zero));
        joint.SetLimits(-0.2, 0.2);
        body.AngularVelocity = initialAngularVelocity;

        for (var step = 0; step < 120; step++) scene.Collisions.Step(1.0 / 120.0);
        return body.Owner.Transform.WorldRotation;
    }

    private static RigidBody2D AddBody(
        Scene scene,
        Vector2D<double> position,
        double inertia = 1.0)
    {
        var owner = new Rectangle2D { Position = position };
        var body = owner.AddComponent(new RigidBody2D
        {
            BodyType = CollisionBodyType2D.Dynamic,
            UseGravity = true,
            AllowSleep = false,
            MomentOfInertia = inertia
        });
        scene.AddChild(owner);
        return body;
    }

    private static Scene CreateScene(Vector2D<double> gravity)
    {
        var scene = new Scene("revolute-joint", new EmptySceneLogic());
        scene.Collisions.Gravity = gravity;
        return scene;
    }

    private static double Length(Vector2D<double> value) =>
        Math.Sqrt(value.X * value.X + value.Y * value.Y);

    private sealed class TestObject : GameObject
    {
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
        internal int CircleCount { get; private set; }
        internal int RectangleCount { get; private set; }

        public Vector2D<int> ViewportSize => new(1280, 720);

        public void DrawCircle(Vector2D<double> center, double radius, Vector4D<float> color) => CircleCount++;

        public void DrawRectangle(
            Vector2D<double> center,
            Vector2D<double> size,
            Vector4D<float> color,
            double rotationRadians = 0.0) => RectangleCount++;

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
