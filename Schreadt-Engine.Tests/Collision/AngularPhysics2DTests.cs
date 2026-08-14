using Schreadt_Engine.Collision;
using Schreadt_Engine.Component;
using Schreadt_Engine.Component.Logic;
using Schreadt_Engine.Component.PreFab;
using Silk.NET.Maths;

namespace Schreadt_Engine.Tests.Collision;

public sealed class AngularPhysics2DTests
{
    [Fact]
    public void InertiaHelpers_CalculateSupportedShapesAndOffsets()
    {
        Assert.Equal(9.0, PhysicsInertia2D.ForSolidCircle(2.0, 3.0), 10);
        Assert.Equal(5.0, PhysicsInertia2D.ForSolidBox(3.0, new Vector2D<double>(2.0, 4.0)), 10);
        Assert.Equal(80.0, PhysicsInertia2D.AtOffset(5.0, 3.0, new Vector2D<double>(3.0, 4.0)), 10);

        Assert.Throws<ArgumentOutOfRangeException>(() => PhysicsInertia2D.ForSolidCircle(0.0, 1.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => PhysicsInertia2D.ForSolidCircle(1.0, double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PhysicsInertia2D.ForSolidBox(1.0, Vector2D<double>.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PhysicsInertia2D.AtOffset(0.0, 1.0, Vector2D<double>.Zero));
    }

    [Fact]
    public void AngularProperties_ValidateAndClampWithoutPartiallyChangingState()
    {
        var body = new RigidBody2D
        {
            BodyType = CollisionBodyType2D.Dynamic,
            MomentOfInertia = 2.0,
            AngularDamping = 0.3,
            MaximumAngularSpeed = 2.0,
            SleepAngularVelocityThreshold = 0.04
        };
        body.AngularVelocity = 5.0;

        Assert.Equal(2.0, body.AngularVelocity);
        Assert.Throws<ArgumentOutOfRangeException>(() => body.AngularVelocity = double.NaN);
        Assert.Throws<ArgumentOutOfRangeException>(() => body.MomentOfInertia = 0.0);
        Assert.Throws<ArgumentOutOfRangeException>(() => body.AngularDamping = -1.0);
        Assert.Throws<ArgumentOutOfRangeException>(() => body.MaximumAngularSpeed = 0.0);
        Assert.Throws<ArgumentOutOfRangeException>(() => body.SleepAngularVelocityThreshold = double.NaN);

        Assert.Equal(2.0, body.AngularVelocity);
        Assert.Equal(2.0, body.MomentOfInertia);
        Assert.Equal(0.3, body.AngularDamping);
        Assert.Equal(2.0, body.MaximumAngularSpeed);
        Assert.Equal(0.04, body.SleepAngularVelocityThreshold);
    }

    [Fact]
    public void Torque_UsesMomentOfInertiaAndIntegratesWorldRotation()
    {
        var scene = CreateScene();
        var (actor, body, _) = AddDynamicBox(scene, Vector2D<double>.Zero, Vector2D<double>.One);
        body.MomentOfInertia = 2.0;
        body.AddTorque(4.0);

        scene.Collisions.Step(0.5);

        Assert.Equal(1.0, body.AngularVelocity, 10);
        Assert.Equal(0.5, actor.Transform.WorldRotation, 10);
        Assert.Equal(0.0, body.AccumulatedTorque);
    }

    [Fact]
    public void AngularDamping_ReducesVelocityBeforeRotationIntegration()
    {
        var scene = CreateScene();
        var (actor, body, _) = AddDynamicBox(scene, Vector2D<double>.Zero, Vector2D<double>.One);
        body.AngularVelocity = 2.0;
        body.AngularDamping = Math.Log(2.0);

        scene.Collisions.Step(1.0);

        Assert.Equal(1.0, body.AngularVelocity, 10);
        Assert.Equal(1.0, actor.RotationRadians, 10);
    }

    [Fact]
    public void OffCenterImpulse_ChangesLinearAndAngularVelocity()
    {
        var scene = CreateScene();
        var (actor, body, _) = AddDynamicBox(scene, Vector2D<double>.Zero, Vector2D<double>.One);
        body.Mass = 2.0;
        body.MomentOfInertia = 4.0;

        body.AddImpulseAtPoint(new Vector2D<double>(0.0, 4.0), actor.Position + Vector2D<double>.UnitX);

        Assert.Equal(new Vector2D<double>(0.0, 2.0), body.Velocity);
        Assert.Equal(1.0, body.AngularVelocity, 10);
        Assert.Equal(new Vector2D<double>(-1.0, 2.0),
            body.GetVelocityAtPoint(actor.Position + Vector2D<double>.UnitY));
    }

    [Fact]
    public void CenteredImpulse_DoesNotIntroduceAngularVelocity()
    {
        var scene = CreateScene();
        var (actor, body, _) = AddDynamicBox(scene, Vector2D<double>.Zero, Vector2D<double>.One);

        body.AddImpulseAtPoint(new Vector2D<double>(3.0, -2.0), actor.Position);

        Assert.Equal(new Vector2D<double>(3.0, -2.0), body.Velocity);
        Assert.Equal(0.0, body.AngularVelocity);
    }

    [Fact]
    public void FixedRotation_ClearsAndRejectsAngularMotionButKeepsLinearImpulse()
    {
        var scene = CreateScene();
        var (actor, body, _) = AddDynamicBox(scene, Vector2D<double>.Zero, Vector2D<double>.One);
        body.AngularVelocity = 1.0;
        body.AddTorque(2.0);

        body.FixedRotation = true;
        body.AddTorque(4.0);
        body.AddAngularImpulse(3.0);
        body.AddImpulseAtPoint(new Vector2D<double>(2.0, 0.0), actor.Position + Vector2D<double>.UnitY);
        scene.Collisions.Step(0.5);

        Assert.Equal(0.0, body.AngularVelocity);
        Assert.Equal(0.0, body.AccumulatedTorque);
        Assert.Equal(0.0, actor.Transform.WorldRotation);
        Assert.Equal(2.0, body.Velocity.X, 10);
    }

    [Fact]
    public void KinematicBody_IntegratesConfiguredAngularVelocityWithoutCollisionResponse()
    {
        var scene = CreateScene();
        var actor = new Rectangle2D { Size = Vector2D<double>.One };
        var body = actor.AddComponent(new RigidBody2D
        {
            BodyType = CollisionBodyType2D.Kinematic,
            AngularVelocity = 1.5
        });
        actor.AddComponent(new OrientedBoxCollider2D(actor.Size));
        scene.AddChild(actor);

        scene.Collisions.Step(0.4);

        Assert.Equal(0.6, actor.RotationRadians, 10);
        Assert.Equal(1.5, body.AngularVelocity, 10);
    }

    [Fact]
    public void AngularIntegration_PreservesParentRotationAndUpdatesChildLocalRotation()
    {
        var scene = CreateScene();
        var parent = new Rectangle2D { RotationRadians = 0.4 };
        var child = new Rectangle2D { Transform = { LocalRotation = 0.2 } };
        var body = child.AddComponent(new RigidBody2D
        {
            BodyType = CollisionBodyType2D.Dynamic,
            AngularVelocity = 1.0
        });
        child.AddComponent(new OrientedBoxCollider2D(Vector2D<double>.One));
        parent.AddChild(child);
        scene.AddChild(parent);

        scene.Collisions.Step(0.5);

        Assert.Equal(0.4, parent.RotationRadians, 10);
        Assert.Equal(1.1, child.RotationRadians, 10);
        Assert.Equal(0.7, child.Transform.LocalRotation, 10);
        Assert.Equal(1.0, body.AngularVelocity, 10);
    }

    [Fact]
    public void AngularSleepThreshold_SleepsAndAngularImpulseWakesBody()
    {
        var scene = CreateScene();
        var (_, body, _) = AddDynamicBox(scene, Vector2D<double>.Zero, Vector2D<double>.One);
        body.AllowSleep = true;
        body.AngularVelocity = 0.01;
        body.SleepVelocityThreshold = 0.02;
        body.SleepAngularVelocityThreshold = 0.02;
        body.TimeToSleep = 0.09;

        scene.Collisions.Step(0.1);

        Assert.True(body.IsSleeping);
        Assert.Equal(0.0, body.AngularVelocity);

        body.AddAngularImpulse(0.1);

        Assert.False(body.IsSleeping);
        Assert.True(body.AngularVelocity > 0.0);
    }

    [Fact]
    public void OffCenterCollision_WakesSleepingBody()
    {
        var scene = CreateScene();
        var (_, sleepingBody, _) = AddDynamicBox(
            scene,
            new Vector2D<double>(0.0, 0.35),
            Vector2D<double>.One);
        sleepingBody.AllowSleep = true;
        sleepingBody.MomentOfInertia = 0.1;
        sleepingBody.Sleep();

        var movingObject = new Rectangle2D
        {
            Position = new Vector2D<double>(0.8, 0.0),
            Size = Vector2D<double>.One
        };
        movingObject.AddComponent(new RigidBody2D
        {
            BodyType = CollisionBodyType2D.Kinematic,
            Velocity = new Vector2D<double>(-2.0, 0.0)
        });
        movingObject.AddComponent(new AxisAlignedBoxCollider2D(movingObject.Size));
        scene.AddChild(movingObject);

        scene.Collisions.Step(0.0);

        Assert.False(sleepingBody.IsSleeping);
        Assert.True(sleepingBody.AngularVelocity < 0.0);
    }

    [Fact]
    public void OffCenterCollision_ProducesAngularVelocityAndReportsContactPoint()
    {
        var scene = CreateScene();
        var (_, body, dynamicBox) = AddDynamicBox(
            scene,
            new Vector2D<double>(0.0, 0.35),
            Vector2D<double>.One);
        body.Velocity = new Vector2D<double>(2.0, 0.0);
        body.MomentOfInertia = 0.1;
        body.Friction = 0.0;
        var staticBox = AddStaticBox(scene, new Vector2D<double>(0.8, 0.0), Vector2D<double>.One);
        CollisionContact2D? contact = null;
        dynamicBox.CollisionEntered += value => contact = value;

        scene.Collisions.Step(0.0);

        Assert.True(body.AngularVelocity < 0.0);
        Assert.True(body.Velocity.X < 2.0);
        Assert.True(contact.HasValue);
        Assert.Same(staticBox, contact.Value.Other);
        Assert.True(double.IsFinite(contact.Value.Point.X));
        Assert.True(double.IsFinite(contact.Value.Point.Y));
    }

    [Fact]
    public void CenteredCollision_DoesNotIntroduceSignificantAngularVelocity()
    {
        var scene = CreateScene();
        var (_, body, _) = AddDynamicBox(scene, Vector2D<double>.Zero, Vector2D<double>.One);
        body.Velocity = new Vector2D<double>(2.0, 0.0);
        body.MomentOfInertia = 0.1;
        body.Friction = 0.0;
        AddStaticBox(scene, new Vector2D<double>(0.8, 0.0), Vector2D<double>.One);

        scene.Collisions.Step(0.0);

        Assert.Equal(0.0, body.AngularVelocity, 10);
    }

    [Fact]
    public void ContactFriction_ChangesLinearAndAngularMotion()
    {
        var scene = CreateScene();
        var circle = new Circle { Position = new Vector2D<double>(0.0, 0.45), Radius = 0.5 };
        var body = circle.AddComponent(new RigidBody2D
        {
            BodyType = CollisionBodyType2D.Dynamic,
            Velocity = new Vector2D<double>(1.0, -1.0),
            MomentOfInertia = PhysicsInertia2D.ForSolidCircle(1.0, circle.Radius),
            Friction = 1.0
        });
        circle.AddComponent(new CircleCollider2D(circle.Radius));
        scene.AddChild(circle);
        var floor = AddStaticBox(scene, new Vector2D<double>(0.0, -0.5), new Vector2D<double>(4.0, 1.0));
        floor.Body.Friction = 1.0;

        scene.Collisions.Step(0.0);

        Assert.InRange(body.Velocity.X, 0.0, 0.9999999999);
        Assert.True(body.AngularVelocity < 0.0);
    }

    [Fact]
    public void FallingBox_LandsOnEdgeRotatesAndSettles()
    {
        var scene = CreateScene();
        scene.Collisions.Gravity = new Vector2D<double>(0.0, -9.81);
        var (actor, body, _) = AddDynamicBox(
            scene,
            new Vector2D<double>(0.0, 2.0),
            new Vector2D<double>(1.0, 0.5));
        actor.RotationRadians = 0.35;
        body.AllowSleep = true;
        body.Friction = 0.8;
        body.LinearDamping = 0.2;
        body.AngularDamping = 2.0;
        body.SleepVelocityThreshold = 0.15;
        body.SleepAngularVelocityThreshold = 0.15;
        body.TimeToSleep = 0.2;
        var floor = AddStaticBox(scene, new Vector2D<double>(0.0, -0.5), new Vector2D<double>(8.0, 1.0));
        floor.Body.Friction = 0.8;
        var initialRotation = actor.RotationRadians;
        var rotatedAfterContact = false;

        for (var step = 0; step < 1200 && !body.IsSleeping; step++)
        {
            scene.Collisions.Step(1.0 / 120.0);
            rotatedAfterContact |= Math.Abs(actor.RotationRadians - initialRotation) > 0.01;
        }

        Assert.True(rotatedAfterContact);
        Assert.True(body.IsSleeping);
        Assert.Equal(0.0, body.Velocity.Length, 10);
        Assert.Equal(0.0, body.AngularVelocity);
    }

    [Fact]
    public void SeparatingContact_DoesNotAddLinearOrAngularEnergy()
    {
        var scene = CreateScene();
        var circle = new Circle { Position = new Vector2D<double>(0.0, 0.45), Radius = 0.5 };
        var body = circle.AddComponent(new RigidBody2D
        {
            BodyType = CollisionBodyType2D.Dynamic,
            Velocity = Vector2D<double>.UnitY,
            Restitution = 1.0,
            Friction = 1.0
        });
        circle.AddComponent(new CircleCollider2D(circle.Radius));
        scene.AddChild(circle);
        AddStaticBox(scene, new Vector2D<double>(0.0, -0.5), new Vector2D<double>(4.0, 1.0));

        scene.Collisions.Step(0.0);

        Assert.Equal(Vector2D<double>.UnitY, body.Velocity);
        Assert.Equal(0.0, body.AngularVelocity);
    }

    [Fact]
    public void TriggerContact_ReportsPointWithoutApplyingLinearOrAngularImpulse()
    {
        var scene = CreateScene();
        var (_, body, dynamicBox) = AddDynamicBox(
            scene,
            new Vector2D<double>(0.0, 0.35),
            Vector2D<double>.One);
        body.Velocity = new Vector2D<double>(2.0, 0.0);
        body.MomentOfInertia = 0.1;
        var trigger = AddStaticBox(scene, new Vector2D<double>(0.8, 0.0), Vector2D<double>.One);
        trigger.IsTrigger = true;
        CollisionContact2D? contact = null;
        dynamicBox.CollisionEntered += value => contact = value;

        scene.Collisions.Step(0.0);

        Assert.Equal(new Vector2D<double>(2.0, 0.0), body.Velocity);
        Assert.Equal(0.0, body.AngularVelocity);
        Assert.True(contact.HasValue);
        Assert.True(double.IsFinite(contact.Value.Point.X));
        Assert.True(double.IsFinite(contact.Value.Point.Y));
    }

    [Fact]
    public void BuiltInNarrowPhases_ProvideRepresentativeContactPoints()
    {
        var firstCircle = CreateCircle(Vector2D<double>.Zero, 1.0);
        var secondCircle = CreateCircle(new Vector2D<double>(1.5, 0.0), 1.0);
        Assert.True(new CircleCircleNarrowPhase2D().TryCollide(firstCircle, secondCircle, out var circles));
        Assert.Equal(1, circles.ContactPointCount);
        Assert.Equal(new Vector2D<double>(0.75, 0.0), circles.GetContactPoint(0));

        var firstBox = CreateOrientedBox(Vector2D<double>.Zero, new Vector2D<double>(2.0, 2.0));
        var secondBox = CreateOrientedBox(new Vector2D<double>(1.5, 0.0), new Vector2D<double>(2.0, 2.0));
        Assert.True(new OrientedBoxOrientedBoxNarrowPhase2D().TryCollide(firstBox, secondBox, out var boxes));
        Assert.Equal(1, boxes.ContactPointCount);
        Assert.Equal(new Vector2D<double>(0.75, 0.0), boxes.GetContactPoint(0));
    }

    [Fact]
    public void LegacyCustomNarrowPhase_UsesCenterMidpointAsContactFallback()
    {
        var scene = CreateScene();
        scene.Collisions.RegisterNarrowPhase(new ContactlessNarrowPhase());
        var firstObject = new Rectangle2D { Position = new Vector2D<double>(1.0, 2.0) };
        var first = firstObject.AddComponent(new TestCollider());
        var secondObject = new Rectangle2D { Position = new Vector2D<double>(3.0, 4.0) };
        secondObject.AddComponent(new TestCollider());
        CollisionContact2D? contact = null;
        first.CollisionEntered += value => contact = value;
        scene.AddChild(firstObject);
        scene.AddChild(secondObject);

        scene.Collisions.Step(0.0);

        Assert.True(contact.HasValue);
        Assert.Equal(new Vector2D<double>(2.0, 3.0), contact.Value.Point);
    }

    [Fact]
    public void CollisionResult_ValidatesContactsAndBoundsAccess()
    {
        var result = new CollisionResult2D(
            Vector2D<double>.UnitX,
            0.1,
            new Vector2D<double>(1.0, 2.0),
            new Vector2D<double>(3.0, 4.0));

        Assert.Equal(2, result.ContactPointCount);
        Assert.Equal(new Vector2D<double>(1.0, 2.0), result.GetContactPoint(0));
        Assert.Equal(new Vector2D<double>(3.0, 4.0), result.GetContactPoint(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => result.GetContactPoint(2));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CollisionResult2D(Vector2D<double>.UnitX, 0.1, new Vector2D<double>(double.NaN, 0.0)));
    }

    private static Scene CreateScene()
    {
        var scene = new Scene("angular-physics", new EmptySceneLogic());
        scene.Collisions.Gravity = Vector2D<double>.Zero;
        return scene;
    }

    private static (Rectangle2D Actor, RigidBody2D Body, OrientedBoxCollider2D Collider) AddDynamicBox(
        Scene scene,
        Vector2D<double> position,
        Vector2D<double> size)
    {
        var actor = new Rectangle2D { Position = position, Size = size };
        var body = actor.AddComponent(new RigidBody2D
        {
            BodyType = CollisionBodyType2D.Dynamic,
            AllowSleep = false,
            MomentOfInertia = PhysicsInertia2D.ForSolidBox(1.0, size)
        });
        var collider = actor.AddComponent(new OrientedBoxCollider2D(size));
        scene.AddChild(actor);
        return (actor, body, collider);
    }

    private static AxisAlignedBoxCollider2D AddStaticBox(
        Scene scene,
        Vector2D<double> position,
        Vector2D<double> size)
    {
        var actor = new Rectangle2D { Position = position, Size = size };
        var collider = actor.AddComponent(new AxisAlignedBoxCollider2D(size));
        scene.AddChild(actor);
        return collider;
    }

    private static CircleCollider2D CreateCircle(Vector2D<double> position, double radius)
    {
        var actor = new Circle { Position = position, Radius = radius };
        return actor.AddComponent(new CircleCollider2D(radius));
    }

    private static OrientedBoxCollider2D CreateOrientedBox(
        Vector2D<double> position,
        Vector2D<double> size)
    {
        var actor = new Rectangle2D { Position = position, Size = size };
        return actor.AddComponent(new OrientedBoxCollider2D(size));
    }

    private sealed class TestCollider : Collider2D
    {
        public override Vector2D<double> Center => Owner.Position;
    }

    private sealed class ContactlessNarrowPhase : ICollisionNarrowPhase2D<TestCollider, TestCollider>
    {
        public bool TryCollide(TestCollider first, TestCollider second, out CollisionResult2D result)
        {
            result = new CollisionResult2D(Vector2D<double>.UnitX, 0.1);
            return true;
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
}
