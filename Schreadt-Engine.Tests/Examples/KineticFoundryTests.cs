using Example_Game.Logic;
using Example_Game.Logic.scenes;
using Schreadt_Engine.Collision;
using Schreadt_Engine.Component;
using Schreadt_Engine.Component.Logic;
using Schreadt_Engine.Component.PreFab;
using Schreadt_Engine.Core;
using Silk.NET.Maths;

namespace Schreadt_Engine.Tests.Examples;

[Collection("Engine lifecycle")]
public sealed class KineticFoundryTests
{
    [Fact]
    public void Scene_InitializesAndAuthoritativelyUnlocksTheCompleteRoute()
    {
        EngineMain.Shutdown();
        var logic = new FoundryHarnessGameLogic();
        try
        {
            EngineMain.Init(logic);
            var scene = logic.CapturedContext!.Scenes.CurrentScene!;

            Assert.Equal(3, scene.CompositionPasses.Count);
            Assert.Equal(3, scene.Collisions.Joints.Count);
            Assert.Equal(4, scene.Children.OfType<FoundryDoor>().Count());
            Assert.Equal(6, scene.Children.OfType<FoundryCrate>().Count());
            Assert.Equal(4, scene.Children.OfType<FoundryResetStation>().Count());

            var doors = scene.Children.OfType<FoundryDoor>().OrderBy(door => door.Position.X).ToArray();
            scene.Update(1.0 / 60.0);
            Assert.All(doors, door => Assert.False(door.IsOpen));

            var crates = scene.Children.OfType<FoundryCrate>().ToArray();
            Assert.Single(crates, crate => crate.Body.Mass == 0.45).Position = new Vector2D<double>(4.35, -1.2);
            scene.Update(1.0 / 60.0);
            Assert.True(doors[0].IsOpen);

            var balanceCrates = crates.Where(crate => crate.Body.Mass == 0.65).ToArray();
            Assert.Equal(2, balanceCrates.Length);
            balanceCrates[0].Position = new Vector2D<double>(7.0, -0.85);
            balanceCrates[1].Position = new Vector2D<double>(8.7, -0.85);
            scene.Update(1.0 / 60.0);
            Assert.True(doors[1].IsOpen);

            Assert.Single(crates, crate => crate.Body.Mass == 1.15).Position = new Vector2D<double>(14.25, -1.2);
            scene.Update(1.0 / 60.0);
            Assert.True(doors[2].IsOpen);

            var ignitionCrate = Assert.Single(crates, crate => crate.Body.Mass == 0.9);
            ignitionCrate.Position = new Vector2D<double>(25.0, -0.84);
            var sensor = Assert.Single(scene.Children.OfType<FoundryCrateSensor>());
            sensor.FixedUpdate(1.0 / 120.0);
            Assert.True(sensor.BlockedByCrate);
            Assert.Single(scene.Children.OfType<FoundryIgnitionLever>())
                .Interact(Assert.Single(scene.Children.OfType<PlayerAvatar>()));
            Assert.True(doors[3].IsOpen);
        }
        finally
        {
            EngineMain.Shutdown();
        }
    }

    [Fact]
    public void MaterialCrates_ExposeDistinctMassAndFrictionAndResetAllMotion()
    {
        var light = CreateCrate(0.45, 0.08);
        var heavy = CreateCrate(2.8, 1.35);
        light.Position = new Vector2D<double>(9.0, -4.0);
        light.RotationRadians = 0.7;
        light.Body.Velocity = new Vector2D<double>(3.0, -2.0);
        light.Body.AngularVelocity = 2.5;
        light.Body.AddForce(new Vector2D<double>(2.0, 1.0));

        light.Reset();

        Assert.True(light.Body.Mass < heavy.Body.Mass);
        Assert.True(light.Body.Friction < heavy.Body.Friction);
        Assert.Equal(new Vector2D<double>(1.0, 2.0), light.Position);
        Assert.Equal(0.0, light.RotationRadians);
        Assert.Equal(Vector2D<double>.Zero, light.Body.Velocity);
        Assert.Equal(0.0, light.Body.AngularVelocity);
        Assert.Equal(Vector2D<double>.Zero, light.Body.AccumulatedForce);
    }

    [Fact]
    public void Door_DisablesCollisionBeforeItsOpeningTweenCompletes()
    {
        var door = new FoundryDoor(new Vector2D<double>(3.0, -0.25));

        door.Open();

        Assert.True(door.IsOpen);
        Assert.False(door.Collider.Enabled);
        Assert.Equal(new Vector2D<double>(3.0, -0.25), door.Position);
    }

    [Fact]
    public void Pendulum_ReleaseAndResetRestoreDeclarativeJointState()
    {
        var anchor = new Vector2D<double>(2.0, 2.0);
        var hammer = new FoundryPendulumHammer(new Vector2D<double>(2.0, 1.15), anchor, 0.0);

        hammer.Release();
        hammer.Body.Velocity = new Vector2D<double>(1.0, -2.0);
        hammer.Body.AngularVelocity = 1.5;
        hammer.Reset();

        Assert.False(hammer.Released);
        Assert.Equal(CollisionBodyType2D.Static, hammer.Body.BodyType);
        Assert.Equal(Vector2D<double>.Zero, hammer.Body.Velocity);
        Assert.Equal(0.0, hammer.Body.AngularVelocity);
        Assert.Same(hammer.Body, hammer.Joint.Body);
    }

    [Fact]
    public void Pendulum_ReleasedHammerStrikesTheFloorLevelBlock()
    {
        var scene = new Scene("foundry-hammer-strike", new EmptySceneLogic());
        scene.Collisions.Gravity = new Vector2D<double>(0.0, -11.5);
        var anchor = new Vector2D<double>(0.0, 0.45);
        const double startRotation = -0.82;
        var rotatedLocalAnchor = new Vector2D<double>(
            -Math.Sin(startRotation) * 0.85,
            Math.Cos(startRotation) * 0.85);
        var hammer = new FoundryPendulumHammer(anchor - rotatedLocalAnchor, anchor, startRotation);
        var block = new FoundryCrate(
            new Vector2D<double>(1.25, -1.18),
            1.15,
            0.42,
            new Vector4D<float>(0.66f, 0.28f, 0.18f, 1.0f),
            new Vector2D<double>(0.66, 0.66));
        var plate = new FoundryPressurePlate(new Vector2D<double>(1.95, -1.5), block);
        var floor = new Rectangle2D
        {
            Position = new Vector2D<double>(0.5, -1.85),
            Size = new Vector2D<double>(5.0, 0.6)
        };
        floor.AddComponent(new AxisAlignedBoxCollider2D(floor.Size)
        {
            CollisionLayer = ExampleCollisionLayers.World,
            CollisionMask = ExampleCollisionLayers.WorldMask
        });
        scene.AddChild(floor);
        scene.AddChild(hammer);
        scene.AddChild(block);
        scene.AddChild(plate);
        scene.Init();
        hammer.Release();
        var maximumRightwardSpeed = 0.0;
        var maximumAngularSpeed = 0.0;

        for (var step = 0; step < 480; step++)
        {
            scene.Collisions.Step(1.0 / 240.0);
            maximumRightwardSpeed = Math.Max(maximumRightwardSpeed, block.Body.Velocity.X);
            maximumAngularSpeed = Math.Max(maximumAngularSpeed, Math.Abs(block.Body.AngularVelocity));
        }

        Assert.True(maximumRightwardSpeed > 0.25, $"Block rightward speed only reached {maximumRightwardSpeed}.");
        Assert.True(maximumAngularSpeed > 0.1, $"Block angular speed only reached {maximumAngularSpeed}.");
        Assert.True(plate.Evaluate(), $"Block stopped before the plate at X={block.Position.X}.");
    }

    [Fact]
    public void Rotor_UsesOneKinematicTransformForVisualAndPhysicalArms()
    {
        var rotor = new FoundryRotor(Vector2D<double>.Zero);

        Assert.Equal(2, rotor.Arms.Count);
        Assert.All(rotor.Arms, arm => Assert.Equal(CollisionBodyType2D.Kinematic, arm.BodyType));
        Assert.Equal(2, rotor.Children.OfType<Schreadt_Engine.Component.PreFab.Rectangle2D>().Count());
    }

    [Fact]
    public void IgnitionCrate_CanBePushedFromFloorAcrossRampOntoSeesaw()
    {
        var scene = new Scene("foundry-ignition-ramp", new EmptySceneLogic());
        scene.Collisions.Gravity = new Vector2D<double>(0.0, -11.5);
        var floor = new Rectangle2D
        {
            Position = new Vector2D<double>(1.5, -1.85),
            Size = new Vector2D<double>(6.0, 0.6)
        };
        floor.AddComponent(new AxisAlignedBoxCollider2D(floor.Size)
        {
            CollisionLayer = ExampleCollisionLayers.World,
            CollisionMask = ExampleCollisionLayers.WorldMask
        });
        var ramp = new FoundryRamp(
            new Vector2D<double>(0.0, -1.55),
            new Vector2D<double>(2.2, -1.10));
        var seesaw = new FoundrySeesaw(
            new Vector2D<double>(2.0, 0.16),
            new Vector2D<double>(3.2, -1.18));
        var crate = new FoundryCrate(
            new Vector2D<double>(-0.6, -1.18),
            0.9,
            0.68,
            new Vector4D<float>(0.84f, 0.46f, 0.12f, 1.0f));
        scene.AddChild(floor);
        scene.AddChild(ramp);
        scene.AddChild(seesaw);
        scene.AddChild(crate);
        scene.Init();
        var reachedSeesaw = false;

        for (var step = 0; step < 720; step++)
        {
            crate.Body.AddForce(new Vector2D<double>(10.0, 0.0));
            scene.Collisions.Step(1.0 / 120.0);
            reachedSeesaw |= crate.Position.X is >= 2.35 and <= 4.2 && crate.Position.Y > -1.2;
        }

        Assert.True(
            reachedSeesaw,
            $"Crate never reached the seesaw; final position was {crate.Position}.");
    }

    [Fact]
    public void IgnitionLever_RejectsUnpreparedSensorAndCommitsBeforeCallingEffects()
    {
        var player = new PlayerAvatar(new PlatformerPlayerBehavior(new TestInputState(), Vector2D<double>.Zero));
        var sensorBlocked = false;
        var callbackObservedCommittedState = false;
        FoundryIgnitionLever? lever = null;
        lever = new FoundryIgnitionLever(
            () => sensorBlocked,
            () => callbackObservedCommittedState = lever!.Ignited);

        lever.Interact(player);
        sensorBlocked = true;
        lever.Interact(player);

        Assert.True(lever.Ignited);
        Assert.True(callbackObservedCommittedState);
    }

    [Fact]
    public void Effects_RegisterAtAllThreeCompositionStages()
    {
        var effects = new KineticFoundryEffects();

        Assert.Equal(FrameCompositionStage.BeforeScene, effects.Heat.Stage);
        Assert.Equal(FrameCompositionStage.AfterScene, effects.Sparks.Stage);
        Assert.Equal(FrameCompositionStage.BeforeGui, effects.Flash.Stage);
        Assert.False(effects.Flash.Enabled);

        effects.TriggerIgnition();

        Assert.True(effects.Flash.Enabled);
    }

    [Fact]
    public void Effects_UseOneSeamlessSharedLoopWithoutIgnitionPositionJumps()
    {
        var effects = new KineticFoundryEffects();
        effects.Update(1.37);
        var initialSparkOffset = effects.SparkOffset;
        var initialHeatPulse = effects.HeatPulse;

        effects.TriggerIgnition();
        Assert.Equal(initialSparkOffset, effects.SparkOffset, 12);
        effects.Update(KineticFoundryEffects.CycleDuration);

        Assert.Equal(initialSparkOffset, effects.SparkOffset, 12);
        Assert.Equal(initialHeatPulse, effects.HeatPulse, 12);

        effects = new KineticFoundryEffects();
        effects.Update(KineticFoundryEffects.CycleDuration - 0.01);
        var beforeWrap = effects.SparkOffset;
        effects.Update(0.02);
        var afterWrap = effects.SparkOffset;
        var circularAdvance =
            (afterWrap - beforeWrap + KineticFoundryEffects.SparkTravel) % KineticFoundryEffects.SparkTravel;
        var expectedAdvance = 0.02 * KineticFoundryEffects.SparkTravel / KineticFoundryEffects.CycleDuration;

        Assert.Equal(expectedAdvance, circularAdvance, 12);
    }

    [Fact]
    public void Sensors_RenderSafelyWhenRayStartsInsideTarget()
    {
        var scene = new Scene("foundry-sensor-origin", new EmptySceneLogic());
        scene.Collisions.Gravity = Vector2D<double>.Zero;
        var rotor = new FoundryRotor(Vector2D<double>.Zero);
        var safetySensor = new FoundrySafetySensor(Vector2D<double>.Zero, rotor);
        var player = new TestActor();
        player.AddComponent(new CircleCollider2D(0.3)
        {
            CollisionLayer = ExampleCollisionLayers.Player,
            CollisionMask = CollisionLayerMask2D.All
        });
        var crate = CreateCrate(0.9, 0.68);
        crate.Position = Vector2D<double>.Zero;
        var crateSensor = new FoundryCrateSensor(Vector2D<double>.Zero, crate);
        scene.AddChild(rotor);
        scene.AddChild(safetySensor);
        scene.AddChild(player);
        scene.AddChild(crate);
        scene.AddChild(crateSensor);
        scene.Init();

        safetySensor.FixedUpdate(1.0 / 120.0);
        crateSensor.FixedUpdate(1.0 / 120.0);

        Assert.True(safetySensor.Obstructed);
        Assert.True(crateSensor.BlockedByCrate);
        var renderer = new PositiveShapeRenderContext();
        safetySensor.Render(renderer);
        crateSensor.Render(renderer);
        Assert.Equal(2, renderer.CircleCount);
        Assert.Equal(0, renderer.RectangleCount);
    }

    private static FoundryCrate CreateCrate(double mass, double friction) => new(
        new Vector2D<double>(1.0, 2.0),
        mass,
        friction,
        new Vector4D<float>(1.0f, 0.5f, 0.1f, 1.0f));

    private sealed class FoundryHarnessGameLogic : GameLogic
    {
        internal IEngineContext? CapturedContext { get; private set; }

        public override void Init()
        {
            CapturedContext = Context;
            Context.Scenes.RegisterScene("foundry-test", () => new Scene5(Context.Input));
            Context.Scenes.LoadScene("foundry-test");
        }

        public override void Update(double dt)
        {
        }
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

    private sealed class PositiveShapeRenderContext : IRenderContext2D
    {
        internal int CircleCount { get; private set; }
        internal int RectangleCount { get; private set; }
        public Vector2D<int> ViewportSize => new(1280, 720);

        public void DrawCircle(Vector2D<double> center, double radius, Vector4D<float> color)
        {
            Assert.True(radius > 0.0);
            CircleCount++;
        }

        public void DrawRectangle(
            Vector2D<double> center,
            Vector2D<double> size,
            Vector4D<float> color,
            double rotationRadians = 0.0)
        {
            Assert.True(size.X > 0.0);
            Assert.True(size.Y > 0.0);
            RectangleCount++;
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
        public bool Available => true;
        public System.Numerics.Vector2 MousePosition => default;
        public System.Numerics.Vector2 MouseDelta => default;
        public System.Numerics.Vector2 ScrollDelta => default;
        public Vector2D<double> MouseViewportPosition => default;
        public double ViewportAspectRatio => 16.0 / 9.0;
        public string TextInput => string.Empty;
        public event Action<InputKey>? KeyPressed { add { } remove { } }
        public event Action<InputKey>? KeyReleased { add { } remove { } }
        public event Action<char>? CharacterTyped { add { } remove { } }
        public event Action<InputMouseButton>? MouseButtonPressed { add { } remove { } }
        public event Action<InputMouseButton>? MouseButtonReleased { add { } remove { } }
        public event Action<System.Numerics.Vector2>? MouseMoved { add { } remove { } }
        public event Action<System.Numerics.Vector2>? Scrolled { add { } remove { } }
        public bool IsKeyDown(InputKey key) => false;
        public bool WasKeyPressed(InputKey key) => false;
        public bool WasKeyReleased(InputKey key) => false;
        public bool IsMouseButtonDown(InputMouseButton button) => false;
        public bool WasMouseButtonPressed(InputMouseButton button) => false;
        public bool WasMouseButtonReleased(InputMouseButton button) => false;
        public bool IsActionDown(string action) => false;
        public bool WasActionPressed(string action) => false;
        public bool WasActionReleased(string action) => false;
    }
}
