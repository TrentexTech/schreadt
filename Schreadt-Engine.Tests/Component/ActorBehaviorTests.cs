using Schreadt_Engine.Component;
using Schreadt_Engine.Component.Logic;

namespace Schreadt_Engine.Tests.Component;

public sealed class ActorBehaviorTests
{
    [Fact]
    public void ActorBehavior_RejectsNonActorOwnerAndRollsBackAttachment()
    {
        var gameObject = new TestObject();
        var behavior = new RecordingBehavior([]);

        var exception = Assert.Throws<InvalidOperationException>(() => gameObject.AddComponent(behavior));

        Assert.Contains("only be attached to an Actor", exception.Message);
        Assert.False(behavior.Attached);
        Assert.Empty(gameObject.Components);
    }

    [Fact]
    public void ActorBehavior_UsesOrdinaryComponentLifecycleOrder()
    {
        var calls = new List<string>();
        var actor = new RecordingActor(calls);
        var behavior = actor.AddComponent(new RecordingBehavior(calls));
        actor.AddComponent(new RecordingComponent(calls));

        actor.Init();
        actor.Update(0.25);
        actor.FixedUpdate(0.5);
        actor.Shutdown();
        actor.Shutdown();

        Assert.Equal(
        [
            "actor-init",
            "behavior-init",
            "component-init",
            "actor-update",
            "behavior-update",
            "component-update",
            "actor-fixed-update",
            "behavior-fixed-update",
            "component-fixed-update",
            "actor-shutdown",
            "component-shutdown",
            "behavior-shutdown"
        ], calls);
        Assert.Same(behavior, actor.GetComponent<RecordingBehavior>());
        Assert.Equal(1, behavior.ShutdownCount);
    }

    [Fact]
    public void ActorBehavior_AdditionAndRemovalDuringUpdateUseComponentSnapshotSemantics()
    {
        var actor = new TestActor();
        var addedBehavior = new CountingBehavior();
        var mutatingBehavior = actor.AddComponent(new MutatingBehavior(addedBehavior));
        actor.Init();

        actor.Update(0.1);

        Assert.False(mutatingBehavior.Attached);
        Assert.Equal(1, mutatingBehavior.UpdateCount);
        Assert.Equal(1, mutatingBehavior.ShutdownCount);
        Assert.True(addedBehavior.Attached);
        Assert.Equal(1, addedBehavior.InitCount);
        Assert.Equal(0, addedBehavior.UpdateCount);

        actor.Update(0.1);
        actor.Shutdown();

        Assert.Equal(1, addedBehavior.UpdateCount);
        Assert.Equal(1, addedBehavior.ShutdownCount);
        Assert.Equal(1, mutatingBehavior.ShutdownCount);
    }

    [Fact]
    public void ActorBehavior_FailedInitializationRollsBackComponentOwnership()
    {
        var initializedActor = new TestActor();
        var uninitializedActor = new TestActor();
        var behavior = new FailingInitializationBehavior();
        initializedActor.Init();

        var exception = Assert.Throws<InvalidOperationException>(() => initializedActor.AddComponent(behavior));

        Assert.Equal("Injected behavior initialization failure.", exception.Message);
        Assert.False(behavior.Attached);
        Assert.Equal(1, behavior.DetachedCount);
        Assert.DoesNotContain(behavior, initializedActor.Components);

        uninitializedActor.AddComponent(behavior);
        Assert.True(behavior.Attached);
        Assert.Same(behavior, uninitializedActor.GetComponent<FailingInitializationBehavior>());
    }

    private sealed class TestObject : GameObject;

    private sealed class TestActor : Actor;

    private sealed class RecordingActor(List<string> calls) : Actor
    {
        protected override void OnInit() => calls.Add("actor-init");

        protected override void OnUpdate(double dt) => calls.Add("actor-update");

        protected override void OnFixedUpdate(double dt) => calls.Add("actor-fixed-update");

        protected override void OnShutdown() => calls.Add("actor-shutdown");
    }

    private sealed class RecordingBehavior(List<string> calls) : ActorBehavior
    {
        internal int ShutdownCount { get; private set; }

        public override void Init() => calls.Add("behavior-init");

        public override void Update(double dt) => calls.Add("behavior-update");

        public override void FixedUpdate(double dt) => calls.Add("behavior-fixed-update");

        public override void Shutdown()
        {
            ShutdownCount++;
            calls.Add("behavior-shutdown");
        }
    }

    private sealed class RecordingComponent(List<string> calls) : GameComponent,
        IInitializable, IUpdateable, IFixedUpdateable, IShutdownable
    {
        public void Init() => calls.Add("component-init");

        public void Update(double dt) => calls.Add("component-update");

        public void FixedUpdate(double dt) => calls.Add("component-fixed-update");

        public void Shutdown() => calls.Add("component-shutdown");
    }

    private class CountingBehavior : ActorBehavior
    {
        protected int UpdateCounter;

        internal int InitCount { get; private set; }
        internal int UpdateCount => UpdateCounter;
        internal int ShutdownCount { get; private set; }

        public override void Init() => InitCount++;

        public override void Update(double dt) => UpdateCounter++;

        public override void Shutdown() => ShutdownCount++;
    }

    private sealed class MutatingBehavior(CountingBehavior addedBehavior) : CountingBehavior
    {
        public override void Update(double dt)
        {
            UpdateCounter++;
            Actor.AddComponent(addedBehavior);
            Assert.True(Actor.RemoveComponent(this));
        }
    }

    private sealed class FailingInitializationBehavior : ActorBehavior
    {
        internal int DetachedCount { get; private set; }

        public override void Init() => throw new InvalidOperationException("Injected behavior initialization failure.");

        public override void Update(double dt)
        {
        }

        protected override void OnDetached() => DetachedCount++;
    }
}