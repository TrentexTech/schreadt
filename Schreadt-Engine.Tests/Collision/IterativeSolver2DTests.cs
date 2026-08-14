using Schreadt_Engine.Collision;
using Schreadt_Engine.Component;
using Schreadt_Engine.Component.Logic;
using Schreadt_Engine.Component.PreFab;
using Silk.NET.Maths;

namespace Schreadt_Engine.Tests.Collision;

public sealed class IterativeSolver2DTests
{
    [Fact]
    public void IterationCounts_AreBoundedAndDoNotPartiallyChangeState()
    {
        var world = new CollisionWorld2D();

        Assert.Equal(3, world.PositionIterations);
        Assert.Equal(8, world.VelocityIterations);

        world.PositionIterations = 5;
        world.VelocityIterations = 12;

        Assert.Throws<ArgumentOutOfRangeException>(() => world.PositionIterations = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => world.PositionIterations = 33);
        Assert.Throws<ArgumentOutOfRangeException>(() => world.VelocityIterations = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => world.VelocityIterations = 33);
        Assert.Equal(5, world.PositionIterations);
        Assert.Equal(12, world.VelocityIterations);
    }

    [Fact]
    public void Step_BuildsEachManifoldOnceAndEmitsOneEventPerPair()
    {
        var scene = CreateScene(Vector2D<double>.Zero);
        scene.Collisions.PositionIterations = 7;
        scene.Collisions.VelocityIterations = 12;
        var narrowPhase = new CountingNarrowPhase();
        scene.Collisions.RegisterNarrowPhase(narrowPhase);

        var firstObject = new Rectangle2D { Position = Vector2D<double>.Zero };
        var first = firstObject.AddComponent(new TestCollider { IsTrigger = true });
        var secondObject = new Rectangle2D { Position = new Vector2D<double>(0.5, 0.0) };
        secondObject.AddComponent(new TestCollider { IsTrigger = true });
        scene.AddChild(firstObject);
        scene.AddChild(secondObject);
        var entered = 0;
        var stayed = 0;
        first.CollisionEntered += _ => entered++;
        first.CollisionStayed += _ => stayed++;

        scene.Collisions.Step(0.0);

        Assert.Equal(1, narrowPhase.CallCount);
        Assert.Equal(1, entered);
        Assert.Equal(0, stayed);
        Assert.Equal(1, scene.Collisions.Statistics.ContactCount);
        Assert.Equal(1, scene.Collisions.Statistics.ContactPointCount);
        Assert.Equal(7, scene.Collisions.Statistics.PositionIterationCount);
        Assert.Equal(12, scene.Collisions.Statistics.VelocityIterationCount);
        Assert.True(double.IsFinite(scene.Collisions.Statistics.SolverMilliseconds));
        Assert.True(scene.Collisions.Statistics.SolverMilliseconds >= 0.0);

        scene.Collisions.Step(0.0);

        Assert.Equal(2, narrowPhase.CallCount);
        Assert.Equal(1, entered);
        Assert.Equal(1, stayed);
    }

    [Fact]
    public void FaceContact_ReportsBothPointsThroughWorldStatistics()
    {
        var scene = CreateScene(Vector2D<double>.Zero);
        AddStaticBox(scene, new Vector2D<double>(0.0, 0.0), new Vector2D<double>(2.0, 2.0));
        AddStaticBox(scene, new Vector2D<double>(1.5, 0.0), new Vector2D<double>(2.0, 2.0));

        scene.Collisions.Step(0.0);

        Assert.Equal(1, scene.Collisions.Statistics.ContactCount);
        Assert.Equal(2, scene.Collisions.Statistics.ContactPointCount);
    }

    [Fact]
    public void ContactEvents_FollowColliderRegistrationAndPairOrder()
    {
        var scene = CreateScene(Vector2D<double>.Zero);
        scene.Collisions.RegisterNarrowPhase(new CountingNarrowPhase());
        var colliders = new List<TestCollider>();
        for (var index = 0; index < 3; index++)
        {
            var actor = new Rectangle2D { Position = new Vector2D<double>(index * 0.1, 0.0) };
            colliders.Add(actor.AddComponent(new TestCollider { IsTrigger = true }));
            scene.AddChild(actor);
        }

        var labels = colliders
            .Select((collider, index) => (collider, label: index.ToString()))
            .ToDictionary(static entry => entry.collider, static entry => entry.label);
        var events = new List<string>();
        foreach (var collider in colliders)
        {
            collider.CollisionEntered += contact =>
                events.Add($"{labels[collider]}>{labels[(TestCollider)contact.Other]}");
        }

        scene.Collisions.Step(0.0);

        Assert.Equal(["0>1", "1>0", "0>2", "2>0", "1>2", "2>1"], events);
    }

    [Fact]
    public void AdditionalVelocityIterations_ImproveTopDownStackWithoutChangingEvents()
    {
        var singlePass = RunTopDownStack(velocityIterations: 1);
        var iterative = RunTopDownStack(velocityIterations: 8);

        Assert.Equal(singlePass.EnteredEventCount, iterative.EnteredEventCount);
        Assert.Equal(6, iterative.EnteredEventCount);
        Assert.True(
            iterative.ResidualDownwardSpeed < singlePass.ResidualDownwardSpeed * 0.35,
            $"Expected iterations to reduce residual downward speed; single={singlePass.ResidualDownwardSpeed}, iterative={iterative.ResidualDownwardSpeed}.");
    }

    [Fact]
    public void RepresentativeStack_SettlesSleepsAndRemainsAsleepUntilDisturbed()
    {
        var scene = CreateScene(new Vector2D<double>(0.0, -9.81));
        AddStaticBox(scene, new Vector2D<double>(0.0, -0.5), new Vector2D<double>(8.0, 1.0));
        var bodies = new[]
        {
            AddDynamicBox(scene, new Vector2D<double>(0.0, 0.55)),
            AddDynamicBox(scene, new Vector2D<double>(0.0, 1.60)),
            AddDynamicBox(scene, new Vector2D<double>(0.0, 2.65))
        };

        for (var step = 0; step < 2400 && bodies.Any(static body => !body.IsSleeping); step++)
            scene.Collisions.Step(1.0 / 120.0);

        Assert.All(bodies, static body => Assert.True(
            body.IsSleeping,
            $"Body at {body.Owner.Position} did not sleep; velocity={body.Velocity}, angular={body.AngularVelocity}."));
        var restingPositions = bodies.Select(static body => body.Owner.Position).ToArray();

        for (var step = 0; step < 120; step++) scene.Collisions.Step(1.0 / 120.0);

        Assert.All(bodies, static body => Assert.True(
            body.IsSleeping,
            $"Body at {body.Owner.Position} woke while undisturbed; velocity={body.Velocity}, angular={body.AngularVelocity}."));
        for (var index = 0; index < bodies.Length; index++)
            Assert.Equal(restingPositions[index], bodies[index].Owner.Position);

        bodies[^1].AddImpulse(new Vector2D<double>(0.4, 0.0));

        Assert.False(bodies[^1].IsSleeping);
    }

    [Fact]
    public void LevelSixStack_ResettlesAfterAnOffCenterHorizontalInteraction()
    {
        var scene = CreateScene(new Vector2D<double>(0.0, -11.5));
        AddStaticBox(scene, new Vector2D<double>(0.0, -0.5), new Vector2D<double>(12.0, 1.0));
        var bodies = new[]
        {
            AddLevelSixCrate(scene, new Vector2D<double>(0.0, 0.23)),
            AddLevelSixCrate(scene, new Vector2D<double>(0.0, 0.70)),
            AddLevelSixCrate(scene, new Vector2D<double>(0.0, 1.17))
        };

        SimulateUntilSleeping(scene, bodies, maximumSteps: 2400);
        Assert.All(bodies, static body => Assert.True(
            body.IsSleeping,
            $"Initial level-six crate at {body.Owner.Position} did not sleep; velocity={body.Velocity}, angular={body.AngularVelocity}."));

        var bottom = bodies[0];
        bottom.AddImpulseAtPoint(
            new Vector2D<double>(0.8, 0.0),
            bottom.Owner.Position + new Vector2D<double>(0.0, 0.15));
        SimulateUntilSleeping(scene, bodies, maximumSteps: 3600);

        Assert.All(bodies, static body => Assert.True(
            body.IsSleeping,
            $"Level-six crate at {body.Owner.Position} kept moving; velocity={body.Velocity}, angular={body.AngularVelocity}."));
    }

    [Fact]
    public void LevelSixCrate_ResettlesAfterAnOffCenterHorizontalInteraction()
    {
        var scene = CreateScene(new Vector2D<double>(0.0, -11.5));
        AddStaticBox(scene, new Vector2D<double>(0.0, -0.5), new Vector2D<double>(12.0, 1.0));
        var body = AddLevelSixCrate(scene, new Vector2D<double>(0.0, 0.23));
        SimulateUntilSleeping(scene, [body], maximumSteps: 2400);
        Assert.True(body.IsSleeping);

        body.AddImpulseAtPoint(
            new Vector2D<double>(0.8, 0.0),
            body.Owner.Position + new Vector2D<double>(0.0, 0.15));
        SimulateUntilSleeping(scene, [body], maximumSteps: 3600);

        Assert.True(
            body.IsSleeping,
            $"Level-six crate at {body.Owner.Position} kept moving; velocity={body.Velocity}, angular={body.AngularVelocity}.");
    }

    private static StackResult RunTopDownStack(int velocityIterations)
    {
        var scene = CreateScene(Vector2D<double>.Zero);
        scene.Collisions.PositionIterations = 1;
        scene.Collisions.VelocityIterations = velocityIterations;
        var bodies = new[]
        {
            AddDynamicBox(scene, new Vector2D<double>(0.0, 2.5), allowSleep: false, fixedRotation: true),
            AddDynamicBox(scene, new Vector2D<double>(0.0, 1.5), allowSleep: false, fixedRotation: true),
            AddDynamicBox(scene, new Vector2D<double>(0.0, 0.5), allowSleep: false, fixedRotation: true)
        };
        var colliders = bodies.Select(static body => body.Owner.GetComponent<OrientedBoxCollider2D>()!).ToArray();
        var floor = AddStaticBox(scene, new Vector2D<double>(0.0, -0.5), new Vector2D<double>(8.0, 1.0));
        var enteredEventCount = 0;
        foreach (var collider in colliders.Append(floor)) collider.CollisionEntered += _ => enteredEventCount++;
        foreach (var body in bodies) body.Velocity = new Vector2D<double>(0.0, -1.0);

        scene.Collisions.Step(0.0);

        var residualDownwardSpeed = bodies.Sum(static body => Math.Max(0.0, -body.Velocity.Y));
        return new StackResult(residualDownwardSpeed, enteredEventCount);
    }

    private static Scene CreateScene(Vector2D<double> gravity)
    {
        var scene = new Scene("iterative-solver", new EmptySceneLogic());
        scene.Collisions.Gravity = gravity;
        return scene;
    }

    private static RigidBody2D AddDynamicBox(
        Scene scene,
        Vector2D<double> position,
        bool allowSleep = true,
        bool fixedRotation = false)
    {
        var size = Vector2D<double>.One;
        var actor = new Rectangle2D { Position = position, Size = size };
        var body = actor.AddComponent(new RigidBody2D
        {
            BodyType = CollisionBodyType2D.Dynamic,
            AllowSleep = allowSleep,
            FixedRotation = fixedRotation,
            MomentOfInertia = PhysicsInertia2D.ForSolidBox(1.0, size),
            Friction = 0.75,
            Restitution = 0.0,
            LinearDamping = 0.4,
            AngularDamping = 0.8,
            SleepVelocityThreshold = 0.04,
            SleepAngularVelocityThreshold = 0.04,
            TimeToSleep = 0.25
        });
        actor.AddComponent(new OrientedBoxCollider2D(size));
        scene.AddChild(actor);
        return body;
    }

    private static RigidBody2D AddLevelSixCrate(Scene scene, Vector2D<double> position)
    {
        const double mass = 0.8;
        var size = new Vector2D<double>(0.62, 0.46);
        var actor = new Rectangle2D { Position = position, Size = size };
        var body = actor.AddComponent(new RigidBody2D
        {
            BodyType = CollisionBodyType2D.Dynamic,
            Mass = mass,
            MomentOfInertia = PhysicsInertia2D.ForSolidBox(mass, size),
            Friction = 0.7,
            Restitution = 0.05,
            LinearDamping = 0.8,
            AngularDamping = 1.2,
            MaximumSpeed = 4.5,
            MaximumAngularSpeed = 7.0,
            SleepVelocityThreshold = 0.08,
            SleepAngularVelocityThreshold = 0.18,
            TimeToSleep = 0.3
        });
        actor.AddComponent(new OrientedBoxCollider2D(size));
        scene.AddChild(actor);
        return body;
    }

    private static void SimulateUntilSleeping(
        Scene scene,
        IReadOnlyCollection<RigidBody2D> bodies,
        int maximumSteps)
    {
        for (var step = 0; step < maximumSteps && bodies.Any(static body => !body.IsSleeping); step++)
            scene.Collisions.Step(1.0 / 120.0);
    }

    private static OrientedBoxCollider2D AddStaticBox(
        Scene scene,
        Vector2D<double> position,
        Vector2D<double> size)
    {
        var actor = new Rectangle2D { Position = position, Size = size };
        var collider = actor.AddComponent(new OrientedBoxCollider2D(size));
        collider.Body.Friction = 0.75;
        scene.AddChild(actor);
        return collider;
    }

    private readonly record struct StackResult(double ResidualDownwardSpeed, int EnteredEventCount);

    private sealed class TestCollider : Collider2D
    {
        public override Vector2D<double> Center => Owner.Position;
    }

    private sealed class CountingNarrowPhase : ICollisionNarrowPhase2D<TestCollider, TestCollider>
    {
        internal int CallCount { get; private set; }

        public bool TryCollide(TestCollider first, TestCollider second, out CollisionResult2D result)
        {
            CallCount++;
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
