using System.Numerics;
using Example_Game.Logic;
using Example_Game.Logic.scenes;
using Schreadt_Engine.Collision;
using Schreadt_Engine.Component;
using Schreadt_Engine.Component.Logic;
using Schreadt_Engine.Component.PreFab;
using Schreadt_Engine.Core;
using Schreadt_Engine.Gui;
using Silk.NET.Maths;

namespace Schreadt_Engine.Tests.Examples;

public sealed class ExampleInteractionTests
{
    [Fact]
    public void Press_ActivatesExactlyOneNearestAvailableTargetAndHoldDoesNotRepeat()
    {
        var fixture = CreateFixture();
        var unavailable = new TestInteractable("UNAVAILABLE")
        {
            Position = new Vector2D<double>(0.35, 0.0),
            Available = false
        };
        var nearest = new TestInteractable("E: USE NEAREST")
        {
            Position = new Vector2D<double>(0.55, 0.0)
        };
        var farther = new TestInteractable("E: USE FARTHER")
        {
            Position = new Vector2D<double>(0.72, 0.0)
        };
        fixture.Scene.AddChild(unavailable);
        fixture.Scene.AddChild(nearest);
        fixture.Scene.AddChild(farther);
        fixture.Scene.Init();
        fixture.Input.InteractPressed = true;
        fixture.Input.InteractDown = true;

        fixture.Scene.Update(1.0 / 60.0);

        Assert.Equal(0, unavailable.InteractionCount);
        Assert.Equal(1, nearest.InteractionCount);
        Assert.Equal(0, farther.InteractionCount);
        Assert.Same(nearest, fixture.Behavior.CurrentTarget);
        Assert.True(fixture.PromptPanel.Visible);
        Assert.Equal("E: USE NEAREST", fixture.PromptLabel.Text);

        fixture.Input.InteractPressed = false;
        fixture.Scene.Update(1.0 / 60.0);

        Assert.Equal(1, nearest.InteractionCount);
    }

    [Fact]
    public void EqualDistanceTargets_PreserveColliderRegistrationOrder()
    {
        var fixture = CreateFixture();
        var first = new TestInteractable("FIRST")
        {
            Position = new Vector2D<double>(0.6, 0.15)
        };
        var second = new TestInteractable("SECOND")
        {
            Position = new Vector2D<double>(0.6, -0.15)
        };
        fixture.Scene.AddChild(first);
        fixture.Scene.AddChild(second);
        fixture.Scene.Init();
        fixture.Input.InteractPressed = true;

        fixture.Scene.Update(1.0 / 60.0);

        Assert.Equal(1, first.InteractionCount);
        Assert.Equal(0, second.InteractionCount);
    }

    [Fact]
    public void FacingDirection_RestrictsTheQueryToThePlayerFacingSide()
    {
        var fixture = CreateFixture();
        var behind = new TestInteractable("E: USE LEFT")
        {
            Position = new Vector2D<double>(-0.65, 0.0)
        };
        fixture.Scene.AddChild(behind);
        fixture.Scene.Init();

        fixture.Scene.Update(1.0 / 60.0);
        Assert.Null(fixture.Behavior.CurrentTarget);

        fixture.Input.MoveLeftDown = true;
        fixture.Scene.Update(1.0 / 60.0);

        Assert.Equal(-1, fixture.Behavior.FacingDirection);
        Assert.Same(behind, fixture.Behavior.CurrentTarget);
    }

    [Fact]
    public void Focus_ClearsForModalScreensInactiveTargetsRemovalAndUnload()
    {
        var fixture = CreateFixture();
        var target = new TestInteractable("E: USE")
        {
            Position = new Vector2D<double>(0.55, 0.0)
        };
        fixture.Scene.AddChild(target);
        fixture.Scene.Init();
        fixture.Scene.Update(1.0 / 60.0);
        Assert.Same(target, fixture.Behavior.CurrentTarget);

        fixture.Scene.Screens.Push(new GuiScreen("modal", new GuiPanel()) { IsModal = true });

        Assert.Null(fixture.Behavior.CurrentTarget);
        Assert.False(fixture.PromptPanel.Visible);

        fixture.Scene.Screens.Pop();
        fixture.Scene.Update(1.0 / 60.0);
        Assert.Same(target, fixture.Behavior.CurrentTarget);

        fixture.Input.Available = false;
        fixture.Scene.Update(1.0 / 60.0);
        Assert.Null(fixture.Behavior.CurrentTarget);

        fixture.Input.Available = true;
        fixture.Scene.Update(1.0 / 60.0);
        Assert.Same(target, fixture.Behavior.CurrentTarget);

        target.Active = false;
        fixture.Scene.Update(1.0 / 60.0);
        Assert.Null(fixture.Behavior.CurrentTarget);

        target.Active = true;
        fixture.Scene.Update(1.0 / 60.0);
        Assert.Same(target, fixture.Behavior.CurrentTarget);

        Assert.True(fixture.Scene.RemoveChild(target));
        fixture.Scene.Update(1.0 / 60.0);
        Assert.Null(fixture.Behavior.CurrentTarget);

        fixture.Scene.Unload();
        Assert.False(fixture.PromptPanel.Visible);
        Assert.Empty(fixture.Scene.Gui.Elements);
    }

    [Fact]
    public void FoundryObjects_UseTheSharedContractWithoutReadingInput()
    {
        var input = new TestInputState();
        var player = new PlayerAvatar(new PlatformerPlayerBehavior(input, Vector2D<double>.Zero));
        var releases = 0;
        var resets = 0;
        var latch = new FoundryLatch(() => releases++);
        var station = new FoundryResetStation("TEST RIG", () => resets++);
        var ignition = new FoundryIgnitionLever(() => false, () => throw new Xunit.Sdk.XunitException("Must not ignite"));

        Assert.IsAssignableFrom<IInteractable2D>(latch);
        Assert.IsAssignableFrom<IInteractable2D>(station);
        Assert.IsAssignableFrom<IInteractable2D>(ignition);

        latch.Interact(player);
        latch.Interact(player);
        station.Interact(player);
        station.Interact(player);
        ignition.Interact(player);

        Assert.True(latch.Released);
        Assert.Equal(1, releases);
        Assert.Equal(2, station.ResetCount);
        Assert.Equal(2, resets);
        Assert.True(ignition.Rejected);
        Assert.Equal("BLOCK THE SENSOR WITH THE CRATE", ignition.InteractionPrompt);
    }

    private static InteractionFixture CreateFixture()
    {
        var scene = new Scene("interaction", new EmptySceneLogic());
        scene.Collisions.Gravity = Vector2D<double>.Zero;
        var input = new TestInputState();
        var promptPanel = scene.Gui.AddPanel();
        var promptLabel = promptPanel.AddLabel(string.Empty);
        var behavior = new PlayerInteractionBehavior(input, promptPanel, promptLabel);
        var player = new PlayerAvatar(
            new PlatformerPlayerBehavior(input, Vector2D<double>.Zero),
            behavior);
        scene.AddChild(player);
        return new InteractionFixture(scene, input, behavior, promptPanel, promptLabel);
    }

    private sealed record InteractionFixture(
        Scene Scene,
        TestInputState Input,
        PlayerInteractionBehavior Behavior,
        GuiPanel PromptPanel,
        GuiLabel PromptLabel);

    private sealed class TestInteractable : Rectangle2D, IInteractable2D
    {
        internal bool Available { get; set; } = true;
        internal int InteractionCount { get; private set; }

        public string InteractionPrompt { get; }

        internal TestInteractable(string prompt)
        {
            InteractionPrompt = prompt;
            AddComponent(new CircleCollider2D(0.2)
            {
                IsTrigger = true,
                CollisionLayer = ExampleCollisionLayers.Interactable,
                CollisionMask = CollisionLayerMask2D.None
            });
        }

        public bool CanInteract(PlayerAvatar player) => Available;

        public void Interact(PlayerAvatar player) => InteractionCount++;
    }

    private sealed class TestInputState : IInputState
    {
        internal bool InteractPressed { get; set; }
        internal bool InteractDown { get; set; }
        internal bool MoveLeftDown { get; set; }

        public bool Available { get; set; } = true;
        public Vector2 MousePosition => default;
        public Vector2 MouseDelta => default;
        public Vector2 ScrollDelta => default;
        public Vector2D<double> MouseViewportPosition => default;
        public double ViewportAspectRatio => 16.0 / 9.0;
        public string TextInput => string.Empty;

        public event Action<InputKey>? KeyPressed { add { } remove { } }
        public event Action<InputKey>? KeyReleased { add { } remove { } }
        public event Action<char>? CharacterTyped { add { } remove { } }
        public event Action<InputMouseButton>? MouseButtonPressed { add { } remove { } }
        public event Action<InputMouseButton>? MouseButtonReleased { add { } remove { } }
        public event Action<Vector2>? MouseMoved { add { } remove { } }
        public event Action<Vector2>? Scrolled { add { } remove { } }

        public bool IsKeyDown(InputKey key) => false;
        public bool WasKeyPressed(InputKey key) => false;
        public bool WasKeyReleased(InputKey key) => false;
        public bool IsMouseButtonDown(InputMouseButton button) => false;
        public bool WasMouseButtonPressed(InputMouseButton button) => false;
        public bool WasMouseButtonReleased(InputMouseButton button) => false;
        public bool IsActionDown(string action) => action switch
        {
            ExampleInputActions.Interact => InteractDown,
            ExampleInputActions.MoveLeft => MoveLeftDown,
            _ => false
        };
        public bool WasActionPressed(string action) =>
            action == ExampleInputActions.Interact && InteractPressed;
        public bool WasActionReleased(string action) => false;
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
