using Example_Game.Logic;
using System.Numerics;
using Schreadt_Engine.Animation.Tweening;
using Schreadt_Engine.Collision;
using Schreadt_Engine.Component;
using Schreadt_Engine.Component.Logic;
using Schreadt_Engine.Component.PreFab;
using Schreadt_Engine.Core;
using Schreadt_Engine.Gui;
using Silk.NET.Maths;

namespace Schreadt_Engine.Tests.Examples;

public sealed class PlatformerTweenTests
{
    [Fact]
    public void StarCollectionTween_DisablesCollisionAndDeactivatesAfterAnimation()
    {
        var star = new StarToken();
        star.Init();

        Assert.True(star.Collect());
        Assert.False(star.Collider.Enabled);
        Assert.True(star.Active);
        Assert.Single(star.GetComponent<TweenPlayer>()!.ActiveTweens);

        star.Update(0.3);

        Assert.False(star.Active);
        Assert.False(star.Collect());
    }

    [Fact]
    public void LaserScanner_RaycastStopsAtWorldAndHitsExposedPlayer()
    {
        var hitCount = 0;
        var scene = new Scene("laser-test", new EmptySceneLogic());
        var scanner = new LaserScanner(0.0, 0.0, 1.0, () => hitCount++);
        var player = new TestActor { Position = new Vector2D<double>(2.0, 0.0) };
        player.AddComponent(new CircleCollider2D(0.2)
        {
            CollisionLayer = ExampleCollisionLayers.Player,
            CollisionMask = CollisionLayerMask2D.All
        });
        var wall = new Rectangle2D
        {
            Position = new Vector2D<double>(1.0, 0.0),
            Size = new Vector2D<double>(0.2, 1.0)
        };
        wall.AddComponent(new AxisAlignedBoxCollider2D(wall.Size)
        {
            CollisionLayer = ExampleCollisionLayers.World,
            CollisionMask = CollisionLayerMask2D.All
        });

        scene.AddChild(scanner);
        scene.AddChild(player);
        scene.AddChild(wall);
        scene.Init();

        scanner.FixedUpdate(1.0 / 60.0);
        Assert.Equal(0, hitCount);

        Assert.True(scene.RemoveChild(wall));
        scanner.FixedUpdate(1.0 / 60.0);
        Assert.Equal(1, hitCount);
        Assert.True(ExampleCollisionLayers.PlayerMask.Contains(ExampleCollisionLayers.Mechanic));
    }

    [Fact]
    public void PressurePlate_DisarmsLaserWhileCrateIsOnPlate()
    {
        var scene = new Scene("pressure-plate-test", new EmptySceneLogic());
        var laser = new LaserScanner(0.0, 0.0, 1.0, () => { });
        var crate = new PushableCrate();
        var plate = new PressurePlate(crate, laser);
        scene.AddChild(laser);
        scene.AddChild(crate);
        scene.AddChild(plate);
        scene.Init();

        scene.Collisions.Step(1.0 / 120.0);

        Assert.True(plate.Pressed);
        Assert.False(laser.Armed);

        crate.Position = new Vector2D<double>(2.0, 0.0);
        scene.Collisions.Step(1.0 / 120.0);

        Assert.False(plate.Pressed);
        Assert.True(laser.Armed);
        Assert.True(ExampleCollisionLayers.WorldMask.Contains(ExampleCollisionLayers.Mechanic));
        laser.Render(new PositiveSizeRenderContext());
    }

    [Fact]
    public void GoalPortal_OrbitingLightUsesHierarchicalTransform()
    {
        var portal = new GoalPortal { Position = new Vector2D<double>(2.0, 3.0) };
        portal.Init();
        var pivot = Assert.Single(portal.Children);
        var light = Assert.Single(pivot.Children);

        Assert.Equal(new Vector2D<double>(2.08, 3.0), light.Position);

        portal.Update(0.75);

        Assert.Equal(2.0, light.Position.X, 10);
        Assert.Equal(3.08, light.Position.Y, 10);
    }

    [Fact]
    public void PlatformerScreens_UseEngineScreenTransitions()
    {
        var scene = new Scene("screen-transition-test", new EmptySceneLogic());

        var pause = PlatformerScreens.CreatePauseScreen(scene);
        PlatformerScreens.ShowVictory(scene, 3, 0);

        var slide = Assert.IsType<SlideScreenTransition>(pause.OpeningTransition);
        Assert.Same(pause.OpeningTransition, pause.ClosingTransition);
        Assert.Equal(GuiSlideDirection.Down, slide.Direction);
        Assert.IsType<FadeToColorScreenTransition>(scene.Screens.Top!.OpeningTransition);
        Assert.True(scene.Screens.IsTransitioning);
    }

    [Fact]
    public void Player_CanJumpWhileStandingOnPushableCrate()
    {
        var input = new TestInputState { JumpDown = true };
        var behavior = new PlatformerPlayerBehavior(input, Vector2D<double>.Zero);
        var player = new PlayerAvatar(behavior)
        {
            Position = new Vector2D<double>(0.0, 0.48)
        };
        var crate = new PushableCrate();
        var scene = new Scene("crate-jump-test", new EmptySceneLogic());
        scene.Collisions.Gravity = Vector2D<double>.Zero;
        scene.AddChild(player);
        scene.AddChild(crate);
        scene.Init();

        scene.Collisions.Step(1.0 / 120.0);
        player.Update(1.0 / 60.0);

        Assert.Equal(5.35, player.GetComponent<RigidBody2D>()!.Velocity.Y, 10);
    }

    private sealed class TestActor : Actor;

    private sealed class EmptySceneLogic : SceneLogic
    {
        public override void Init()
        {
        }

        public override void Update(double dt)
        {
        }
    }

    private sealed class PositiveSizeRenderContext : IRenderContext2D
    {
        public Vector2D<int> ViewportSize => new(1280, 720);

        public void DrawCircle(Vector2D<double> center, double radius, Vector4D<float> color)
        {
            Assert.True(radius > 0.0);
        }

        public void DrawRectangle(
            Vector2D<double> center,
            Vector2D<double> size,
            Vector4D<float> color,
            double rotationRadians = 0.0)
        {
            Assert.True(size.X > 0.0);
            Assert.True(size.Y > 0.0);
        }

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

    private sealed class TestInputState : IInputState
    {
        public bool JumpDown { get; init; }
        public bool Available => true;
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
        public bool IsActionDown(string action) => JumpDown && action == ExampleInputActions.Jump;
        public bool WasActionPressed(string action) => false;
        public bool WasActionReleased(string action) => false;
    }
}
