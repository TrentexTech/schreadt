using Schreadt_Engine.Collision;
using Schreadt_Engine.Component;
using Schreadt_Engine.Component.Logic;
using Schreadt_Engine.Component.PreFab;
using Silk.NET.Maths;

namespace Schreadt_Engine.Tests.Collision;

public sealed class HierarchyActivityTests
{
    [Fact]
    public void ActiveInHierarchy_TracksNestedAncestorsAndReparentingImmediately()
    {
        var inactiveRoot = new TestObject { Active = false };
        var middle = new TestObject { Active = false };
        var child = new TestObject();
        var activeRoot = new TestObject();
        inactiveRoot.AddChild(middle);
        middle.AddChild(child);

        Assert.True(child.Active);
        Assert.False(child.ActiveInHierarchy);

        inactiveRoot.Active = true;
        Assert.False(child.ActiveInHierarchy);

        middle.Active = true;
        Assert.True(child.ActiveInHierarchy);

        Assert.True(middle.RemoveChild(child));
        inactiveRoot.Active = false;
        activeRoot.AddChild(child);

        Assert.True(child.ActiveInHierarchy);

        activeRoot.Active = false;
        Assert.False(child.ActiveInHierarchy);
    }

    [Fact]
    public void InactiveAncestor_DisablesCollisionQueriesCallbacksAndRigidBodyMotion()
    {
        var scene = CreateScene();
        var parent = new TestObject { Active = false };
        var child = new Circle { Radius = 0.5 };
        var body = child.AddComponent(new RigidBody2D
        {
            BodyType = CollisionBodyType2D.Dynamic,
            UseGravity = false,
            Velocity = Vector2D<double>.UnitX,
            AllowSleep = false
        });
        var childCollider = child.AddComponent(new CircleCollider2D(0.5));
        var trigger = new Circle { Radius = 2.0 };
        trigger.AddComponent(new CircleCollider2D(2.0) { IsTrigger = true });
        var entered = 0;
        var exited = 0;
        childCollider.CollisionEntered += _ => entered++;
        childCollider.CollisionExited += _ => exited++;
        parent.AddChild(child);
        scene.AddChild(parent);
        scene.AddChild(trigger);
        scene.Init();

        scene.Collisions.Step(1.0);

        Assert.Equal(Vector2D<double>.Zero, child.Position);
        Assert.Equal(0, entered);
        Assert.DoesNotContain(childCollider, scene.Collisions.OverlapPoint(Vector2D<double>.Zero));
        Assert.Equal(1, scene.Collisions.Statistics.ActiveColliderCount);

        parent.Active = true;
        scene.Collisions.Step(0.0);

        Assert.Equal(1, entered);
        Assert.Contains(childCollider, scene.Collisions.OverlapPoint(Vector2D<double>.Zero));
        Assert.Equal(2, scene.Collisions.Statistics.ActiveColliderCount);

        scene.Collisions.Step(1.0);
        Assert.Equal(Vector2D<double>.UnitX, child.Position);
        Assert.Equal(Vector2D<double>.UnitX, body.Velocity);

        parent.Active = false;
        scene.Collisions.Step(0.0);
        Assert.Equal(1, exited);
    }

    [Fact]
    public void InactiveAncestor_BlocksDirectChildUpdateAndFixedUpdate()
    {
        var parent = new TestObject { Active = false };
        var child = new TestObject();
        parent.AddChild(child);
        parent.Init();

        child.Update(0.25);
        child.FixedUpdate(0.25);

        Assert.Equal(0, child.UpdateCount);
        Assert.Equal(0, child.FixedUpdateCount);

        parent.Active = true;
        child.Update(0.25);
        child.FixedUpdate(0.25);

        Assert.Equal(1, child.UpdateCount);
        Assert.Equal(1, child.FixedUpdateCount);
    }

    private static Scene CreateScene() => new("hierarchy-activity", new EmptySceneLogic());

    private sealed class TestObject : GameObject
    {
        internal int UpdateCount { get; private set; }
        internal int FixedUpdateCount { get; private set; }

        protected override void OnUpdate(double dt) => UpdateCount++;

        protected override void OnFixedUpdate(double dt) => FixedUpdateCount++;
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
