using Example_Game.Logic;
using Example_Game.Logic.scenes;
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
        Assert.IsType<FadeToColorSceneTransition>(ExampleGameLogic.LevelTransition);
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

    [Fact]
    public void TempestStorm_UsesBackgroundAndAllCompositionPassStages()
    {
        var storm = new TempestStorm2D(randomSeed: 7);
        var initialCloudOffset = storm.CloudOffset;

        storm.Update(1.3);

        Assert.True(storm.CloudOffset > initialCloudOffset);
        Assert.True(storm.RainPhase > 0.0);
        Assert.True(storm.LightningIntensity > 0.0);
        Assert.Equal(FrameCompositionStage.BeforeScene, storm.Lightning.Stage);
        Assert.Equal(FrameCompositionStage.AfterScene, storm.Rain.Stage);
        Assert.Equal(FrameCompositionStage.BeforeGui, storm.ScreenFlash.Stage);
        Assert.True(storm.Lightning.Enabled);
        Assert.True(storm.ScreenFlash.Enabled);

        var context = new StormRenderContext();
        storm.Clouds.Render(context);
        storm.Lightning.Render(context);
        storm.Rain.Render(context);
        storm.ScreenFlash.Render(context);

        Assert.True(context.CircleCount >= 3);
        Assert.True(context.WorldRectangleCount >= 1);
        Assert.Contains(context.LineBatchSizes, count => count == 7);
        Assert.Contains(context.LineBatchSizes, count => count == 72);
        Assert.Equal(1, context.ScreenRectangleCount);
    }

    [Fact]
    public void TempestClouds_KeepTheirRowsWhenCameraCrossesACloudBoundary()
    {
        var storm = new TempestStorm2D(randomSeed: 7);
        var firstView = new StormRenderContext(new CameraView2D(
            Vector2D<double>.Zero,
            2.4,
            16.0 / 9.0));
        var shiftedView = new StormRenderContext(new CameraView2D(
            new Vector2D<double>(2.0, 0.0),
            2.4,
            16.0 / 9.0));

        storm.Clouds.Render(firstView);
        storm.Clouds.Render(shiftedView);

        var sharedClouds = firstView.WorldRectangleCenters
            .Select(first => (First: first, Second: shiftedView.WorldRectangleCenters
                .SingleOrDefault(second => Math.Abs(second.X - first.X) < 1e-10)))
            .Where(pair => pair.Second != default)
            .ToArray();
        Assert.True(sharedClouds.Length >= 3);
        Assert.All(sharedClouds, pair => Assert.Equal(pair.First.Y, pair.Second.Y, 10));
    }

    [Fact]
    public void TempestClouds_WrapTheirDriftPhaseWithoutChangingRows()
    {
        var storm = new TempestStorm2D(randomSeed: 7);
        const double timeAcrossWrap = 0.1;
        storm.Update(TempestStorm2D.CloudSpacing / TempestStorm2D.CloudSpeed - timeAcrossWrap * 0.5);
        var beforeWrap = new StormRenderContext();
        storm.Clouds.Render(beforeWrap);

        storm.Update(timeAcrossWrap);
        var afterWrap = new StormRenderContext();
        storm.Clouds.Render(afterWrap);

        var expectedMovement = timeAcrossWrap * TempestStorm2D.CloudSpeed;
        var continuousClouds = beforeWrap.WorldRectangleCenters
            .Where(center => Math.Abs(center.X) < 3.5)
            .Select(before => (Before: before, After: afterWrap.WorldRectangleCenters.Single(
                after => Math.Abs(after.X - (before.X + expectedMovement)) < 1e-10)))
            .ToArray();
        Assert.NotEmpty(continuousClouds);
        Assert.All(continuousClouds, pair => Assert.Equal(pair.Before.Y, pair.After.Y, 10));

        var elapsed = 25.0;
        var normalizedStorm = new TempestStorm2D(randomSeed: 7);
        normalizedStorm.Update(elapsed);
        Assert.Equal(
            elapsed * TempestStorm2D.CloudSpeed % TempestStorm2D.CloudSpacing,
            normalizedStorm.CloudOffset,
            10);
    }

    [Fact]
    public void TempestRain_WrapsAtItsRenderedTravelWithoutJumping()
    {
        var storm = new TempestStorm2D(randomSeed: 7);
        var initial = new StormRenderContext();
        storm.Rain.Render(initial);

        storm.Update(TempestStorm2D.RainTravel / TempestStorm2D.RainSpeed);
        var repeated = new StormRenderContext();
        storm.Rain.Render(repeated);

        Assert.Equal(0.0, storm.RainPhase, 10);
        var initialRain = Assert.Single(initial.LineBatches);
        var repeatedRain = Assert.Single(repeated.LineBatches);
        Assert.Equal(initialRain, repeatedRain);

        const double halfStep = 0.01;
        storm.Update(TempestStorm2D.RainTravel / TempestStorm2D.RainSpeed - halfStep);
        var beforeWrap = new StormRenderContext();
        storm.Rain.Render(beforeWrap);
        storm.Update(halfStep * 2.0);
        var afterWrap = new StormRenderContext();
        storm.Rain.Render(afterWrap);

        var beforeRain = Assert.Single(beforeWrap.LineBatches);
        var afterRain = Assert.Single(afterWrap.LineBatches);
        var expectedMovement = halfStep * 2.0 * TempestStorm2D.RainSpeed;
        Assert.Equal(beforeRain.Count, afterRain.Count);
        for (var index = 0; index < beforeRain.Count; index++)
        {
            var before = beforeWrap.View.WorldToNormalizedDevicePoint(beforeRain[index].Start);
            var after = afterWrap.View.WorldToNormalizedDevicePoint(afterRain[index].Start);
            Assert.Equal(before.X, after.X, 10);

            var actualMovement = after.Y - before.Y;
            var expectedDropMovement = -expectedMovement;
            if (actualMovement > expectedDropMovement + 1.0)
                expectedDropMovement += TempestStorm2D.RainTravel;
            Assert.Equal(expectedDropMovement, actualMovement, 10);
        }
    }

    [Fact]
    public void ProvisionalRotatingBeam_UpdatesCollisionAndRenderingInFixedStep()
    {
        var scene = new Scene("oriented-beam-test", new EmptySceneLogic());
        var beam = new ProvisionalRotatingBeam(
            new Vector2D<double>(2.0, 0.2),
            minimumRotation: -Math.PI / 2.0,
            maximumRotation: Math.PI / 2.0,
            cycleDuration: 4.0);
        var probe = new Circle { Position = new Vector2D<double>(0.0, 0.8), Radius = 0.15 };
        var probeCollider = probe.AddComponent(new CircleCollider2D(probe.Radius));
        var entered = false;
        beam.Collider.CollisionEntered += contact =>
        {
            if (ReferenceEquals(contact.Other, probeCollider)) entered = true;
        };
        scene.AddChild(beam);
        scene.AddChild(probe);
        scene.Init();

        scene.Collisions.Step(0.0);
        Assert.False(entered);

        beam.FixedUpdate(1.0);
        scene.Collisions.Step(0.0);

        Assert.True(entered);
        Assert.Equal(Math.PI / 2.0, beam.RotationRadians, 10);
        Assert.Equal(beam.RotationRadians, beam.Collider.WorldRotation, 10);
        var renderer = new RotationRecordingRenderContext();
        beam.Render(renderer);
        var draw = Assert.Single(renderer.Rectangles);
        Assert.Equal(beam.RotationRadians, draw.Rotation, 10);
        Assert.Equal(beam.Size, draw.Size);
    }

    [Fact]
    public void ProvisionalOrientedPlatform_UsesHierarchyForItsRotatedSurfaceStripe()
    {
        var platform = new ProvisionalOrientedPlatform(
            new Vector2D<double>(2.0, 0.3),
            Math.PI / 4.0)
        {
            Position = new Vector2D<double>(2.0, 3.0)
        };
        platform.Init();
        var stripe = Assert.IsType<Rectangle2D>(Assert.Single(platform.Children));

        Assert.Equal(platform.RotationRadians, platform.Collider.WorldRotation, 10);
        Assert.Equal(platform.RotationRadians, stripe.RotationRadians, 10);
        var expectedStripePosition = platform.Position + Transform2D.Rotate(
            stripe.Transform.LocalPosition,
            platform.RotationRadians);
        Assert.Equal(expectedStripePosition.X, stripe.Position.X, 10);
        Assert.Equal(expectedStripePosition.Y, stripe.Position.Y, 10);
    }

    [Fact]
    public void ProvisionalOrientedCrate_UsesDynamicBodyAndMatchingCollider()
    {
        var crate = new ProvisionalOrientedCrate(0.35);

        Assert.Equal(CollisionBodyType2D.Dynamic, crate.Body.BodyType);
        Assert.Equal(crate.Size, crate.Collider.Size);
        Assert.Equal(crate.RotationRadians, crate.Collider.WorldRotation, 10);
        Assert.True(crate.Collider.CollisionMask.Contains(ExampleCollisionLayers.Player));
        Assert.True(crate.Collider.CollisionMask.Contains(ExampleCollisionLayers.World));
    }

    [Fact]
    public void Player_CanJumpFromProvisionalOrientedCrate()
    {
        var input = new TestInputState { JumpDown = true };
        var behavior = new PlatformerPlayerBehavior(input, Vector2D<double>.Zero);
        var player = new PlayerAvatar(behavior);
        var crate = new ProvisionalOrientedCrate(0.35);
        player.Position = crate.Position + crate.Collider.AxisY *
            (crate.Collider.HalfSize.Y + PlayerAvatar.PlayerRadius - 0.01);
        var scene = new Scene("oriented-crate-jump-test", new EmptySceneLogic());
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

    private sealed class RotationRecordingRenderContext : IRenderContext2D
    {
        internal List<(Vector2D<double> Size, double Rotation)> Rectangles { get; } = [];

        public Vector2D<int> ViewportSize => new(1280, 720);

        public void DrawCircle(Vector2D<double> center, double radius, Vector4D<float> color)
        {
        }

        public void DrawRectangle(
            Vector2D<double> center,
            Vector2D<double> size,
            Vector4D<float> color,
            double rotationRadians = 0.0) => Rectangles.Add((size, rotationRadians));

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

    private sealed class StormRenderContext : IFrameCompositionContext2D, IBackgroundRenderContext2D
    {
        internal StormRenderContext(CameraView2D? view = null)
        {
            View = view ?? new CameraView2D(Vector2D<double>.Zero, 2.4, 16.0 / 9.0);
        }

        public Vector2D<int> ViewportSize => new(1280, 720);
        public CameraView2D View { get; }
        BackgroundView2D IBackgroundRenderContext2D.View
        {
            get
            {
                var (minimum, maximum) = View.GetVisibleBounds();
                return new BackgroundView2D(
                    View.Center,
                    View.RotationRadians,
                    View.OrthographicSize,
                    View.AspectRatio,
                    minimum,
                    maximum);
            }
        }

        internal int CircleCount { get; private set; }
        internal int WorldRectangleCount => WorldRectangleCenters.Count;
        internal int ScreenRectangleCount { get; private set; }
        internal List<Vector2D<double>> WorldRectangleCenters { get; } = [];
        internal List<int> LineBatchSizes { get; } = [];
        internal List<IReadOnlyList<LineSegment2D>> LineBatches { get; } = [];

        public void RenderBackground(IBackground2D background) => background.Render(this);

        public void DrawLines(IReadOnlyList<LineSegment2D> lines, Vector4D<float> color)
        {
            LineBatchSizes.Add(lines.Count);
            LineBatches.Add(lines.ToArray());
        }

        public void DrawCircle(Vector2D<double> center, double radius, Vector4D<float> color) => CircleCount++;

        public void DrawRectangle(
            Vector2D<double> center,
            Vector2D<double> size,
            Vector4D<float> color,
            double rotationRadians = 0.0) => WorldRectangleCenters.Add(center);

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
            Vector4D<float> color) => ScreenRectangleCount++;

        public void DrawScreenPixels(PixelSurface surface, TextureSampling sampling = TextureSampling.Nearest)
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
