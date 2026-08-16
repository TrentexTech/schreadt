using Schreadt_Engine.Animation.Tweening;
using Schreadt_Engine.Collision;
using Schreadt_Engine.Component;
using Schreadt_Engine.Component.Logic;
using Schreadt_Engine.Component.PreFab;
using Schreadt_Engine.Core;
using Schreadt_Engine.Gui;
using Silk.NET.Maths;

namespace Example_Game.Logic.scenes;

internal abstract class PlatformerLevelLogic : SceneLogic
{
    private readonly IInputState _input;
    private readonly string _title;
    private readonly int _number;
    private readonly string? _nextScene;
    private PlatformerPlayerBehavior _playerBehavior = null!;
    private PlayerInteractionBehavior _interactionBehavior = null!;
    private PlayerAvatar _player = null!;
    private GuiLabel _stats = null!;
    private int _stars;
    private bool _finished;

    protected PlatformerLevelLogic(IInputState input, int number, string title, string? nextScene)
    {
        _input = input;
        _number = number;
        _title = title;
        _nextScene = nextScene;
    }

    protected abstract Vector2D<double> SpawnPoint { get; }
    protected virtual Vector2D<double> Gravity => new(0, -11.5);
    protected virtual string? HudNote => null;
    protected virtual CameraBounds2D CameraBounds => new(
        new Vector2D<double>(-1, -2.8),
        new Vector2D<double>(17, 3.4));
    protected PlayerAvatar Player => _player;
    protected PlayerInteractionBehavior PlayerInteraction => _interactionBehavior;
    protected abstract void BuildLevel();

    public override void Init()
    {
        Scene.Background = LevelBackground.Create(_number);
        Scene.Collisions.Gravity = Gravity;

        _playerBehavior = new PlatformerPlayerBehavior(_input, SpawnPoint);
        var interactionPrompt = Scene.Gui.AddPanel();
        interactionPrompt.Position = new Vector2D<float>(470, 660);
        interactionPrompt.Padding = 8;
        interactionPrompt.BackgroundColor = new Vector4D<float>(0.035f, 0.055f, 0.11f, 0.92f);
        var interactionPromptLabel = interactionPrompt.AddLabel(string.Empty);
        interactionPromptLabel.Scale = 1.3f;
        interactionPromptLabel.Color = new Vector4D<float>(1f, 0.84f, 0.28f, 1f);
        _interactionBehavior = new PlayerInteractionBehavior(
            _input,
            interactionPrompt,
            interactionPromptLabel);
        _playerBehavior.StatsChanged += UpdateStats;
        _player = new PlayerAvatar(_playerBehavior, _interactionBehavior)
        {
            Position = SpawnPoint,
            RenderLayer = 30
        };
        Scene.AddChild(_player);

        BuildLevel();
        AddHud();
        ConfigureCamera();
    }

    public override void Update(double dt)
    {
    }

    protected void AddPlatform(double x, double y, double width, double height, Vector4D<float>? color = null)
    {
        var size = new Vector2D<double>(width, height);
        var block = new Rectangle2D
        {
            Position = new Vector2D<double>(x, y),
            Size = size,
            RenderLayer = 10,
            Color = color ?? (_number == 1
                ? new Vector4D<float>(0.3f, 0.43f, 0.37f, 1f)
                : new Vector4D<float>(0.25f, 0.26f, 0.5f, 1f))
        };
        block.AddComponent(new AxisAlignedBoxCollider2D(size)
        {
            CollisionLayer = ExampleCollisionLayers.World,
            CollisionMask = ExampleCollisionLayers.WorldMask
        });
        Scene.AddChild(block);

        Scene.AddChild(new Rectangle2D
        {
            Position = new Vector2D<double>(x, y + height * 0.5 - 0.035),
            Size = new Vector2D<double>(width, 0.09),
            RenderLayer = 11,
            Color = _number == 1
                ? new Vector4D<float>(0.48f, 0.82f, 0.37f, 1f)
                : new Vector4D<float>(0.39f, 0.88f, 0.92f, 1f)
        });
    }

    protected MovingPlatform AddMovingPlatform(
        double x, double y, double width, double minimumX, double maximumX, double speed)
    {
        var platform = new MovingPlatform(new Vector2D<double>(width, 0.28), minimumX, maximumX, speed)
        {
            Position = new Vector2D<double>(x, y),
            Color = new Vector4D<float>(0.46f, 0.36f, 0.76f, 1f),
            RenderLayer = 12
        };
        Scene.AddChild(platform);
        return platform;
    }

    protected void AddUpdraft(double x, double y, double radius)
    {
        var playerBody = _player.GetComponent<RigidBody2D>()
            ?? throw new InvalidOperationException("The platformer player needs a rigid body.");
        var zone = new TriggerZone2D(radius)
        {
            Position = new Vector2D<double>(x, y),
            RenderLayer = 16,
            CollisionLayer = ExampleCollisionLayers.Mechanic,
            CollisionMask = ExampleCollisionLayers.PlayerOnlyMask,
            Color = new Vector4D<float>(0.2f, 0.92f, 1f, 0.13f),
            Filter = candidate => ReferenceEquals(candidate, _player)
        };
        zone.Entered += _ =>
        {
            playerBody.GravityScale = 0.18;
            const double launchVelocity = 4.2;
            if (playerBody.Velocity.Y < launchVelocity)
            {
                playerBody.AddImpulse(new Vector2D<double>(
                    0.0,
                    (launchVelocity - playerBody.Velocity.Y) * playerBody.Mass));
            }
        };
        zone.Stayed += _ => playerBody.AddForce(new Vector2D<double>(0.0, playerBody.Mass * 5.5));
        zone.Exited += _ => playerBody.GravityScale = 1.0;

        var pulse = Tweens.To(
            () => zone.Color,
            value => zone.Color = value,
            new Vector4D<float>(0.38f, 0.96f, 1f, 0.28f),
            0.65);
        pulse.Easing = TweenEasings.SineInOut;
        pulse.LoopMode = TweenLoopMode.Yoyo;
        pulse.RepeatCount = Tween.RepeatForever;
        zone.AddComponent(new TweenPlayer()).Play(pulse);
        Scene.AddChild(zone);

        for (var index = 0; index < 3; index++)
        {
            var arrow = new Triangle
            {
                Position = new Vector2D<double>(x, y - 0.42 + index * 0.3),
                Scale = new Vector2D<double>(0.2, 0.15),
                RenderLayer = 17,
                Color = new Vector4D<float>(0.62f, 0.98f, 1f, 0.72f)
            };
            var rise = Tweens.To(
                () => arrow.Position,
                value => arrow.Position = value,
                arrow.Position + new Vector2D<double>(0.0, 0.5),
                0.8);
            rise.Delay = index * 0.13;
            rise.Easing = TweenEasings.CubicOut;
            rise.RepeatCount = Tween.RepeatForever;
            arrow.AddComponent(new TweenPlayer()).Play(rise);
            Scene.AddChild(arrow);
        }
    }

    protected LaserScanner AddLaserScanner(
        double x,
        double y,
        double startAngle,
        double endAngle,
        double sweepDuration)
    {
        var scanner = new LaserScanner(startAngle, endAngle, sweepDuration, _playerBehavior.HitHazard)
        {
            Position = new Vector2D<double>(x, y),
            RenderLayer = 24
        };
        Scene.AddChild(scanner);
        return scanner;
    }

    protected void AddCratePressurePlate(
        LaserScanner laser,
        double crateX,
        double crateY,
        double plateX,
        double plateY)
    {
        var crate = new PushableCrate
        {
            Position = new Vector2D<double>(crateX, crateY),
            RenderLayer = 21
        };
        var plate = new PressurePlate(crate, laser)
        {
            Position = new Vector2D<double>(plateX, plateY),
            RenderLayer = 18
        };
        Scene.AddChild(crate);
        Scene.AddChild(plate);
    }

    protected void AddSpikes(double centerX, double groundTop, int count)
    {
        const double spacing = 0.25;
        var width = count * spacing;
        for (var index = 0; index < count; index++)
        {
            Scene.AddChild(new Triangle
            {
                Position = new Vector2D<double>(centerX - width * 0.5 + spacing * (index + 0.5), groundTop + 0.13),
                Scale = new Vector2D<double>(0.24, 0.28),
                RenderLayer = 18,
                Color = new Vector4D<float>(1f, 0.27f, 0.32f, 1f)
            });
        }

        var trigger = new BoxTrigger(new Vector2D<double>(width * 0.95, 0.23), ExampleCollisionLayers.Hazard)
        {
            Position = new Vector2D<double>(centerX, groundTop + 0.11)
        };
        void HandleSpikeContact(CollisionContact2D contact)
        {
            if (ReferenceEquals(contact.Other.Owner, _player)) _playerBehavior.HitHazard();
        }

        trigger.Collider.CollisionEntered += HandleSpikeContact;
        trigger.Collider.CollisionStayed += HandleSpikeContact;
        Scene.AddChild(trigger);
    }

    protected void AddStar(double x, double y)
    {
        var star = new StarToken
        {
            Position = new Vector2D<double>(x, y),
            RenderLayer = 22
        };
        star.Collider.CollisionEntered += contact =>
        {
            if (!ReferenceEquals(contact.Other.Owner, _player) || !star.Collect()) return;
            _stars++;
            UpdateStats();
        };
        Scene.AddChild(star);
    }

    protected void AddGoal(double x, double y)
    {
        AddPortalShard(x - 0.55, y + 0.05, -0.18, 0.2);
        AddPortalShard(x + 0.55, y + 0.05, 0.18, -0.2);

        var portal = new GoalPortal
        {
            Position = new Vector2D<double>(x, y),
            RenderLayer = 20
        };
        portal.Collider.CollisionEntered += contact =>
        {
            if (_finished || !ReferenceEquals(contact.Other.Owner, _player)) return;
            _finished = true;
            if (_nextScene is not null)
                Context.Scenes.LoadScene(_nextScene, ExampleGameLogic.LevelTransition);
            else PlatformerScreens.ShowVictory(Scene, _stars, _playerBehavior.Deaths);
        };
        Scene.AddChild(portal);
    }

    private void AddPortalShard(double x, double y, double rotation, double sway)
    {
        var shard = new Rectangle2D
        {
            Position = new Vector2D<double>(x, y),
            Size = new Vector2D<double>(0.1, 0.7),
            RotationRadians = rotation,
            RenderLayer = 19,
            Color = new Vector4D<float>(0.42f, 0.9f, 1f, 0.72f)
        };
        ExampleTweens.AddPanelSway(shard, sway);
        Scene.AddChild(shard);
    }

    protected void AddEnemy(double x, double y, double minimumX, double maximumX, double speed)
    {
        var enemy = new PatrolEnemy(minimumX, maximumX, speed)
        {
            Position = new Vector2D<double>(x, y),
            RenderLayer = 25
        };
        void HandleEnemyContact(CollisionContact2D contact)
        {
            if (ReferenceEquals(contact.Other.Owner, _player)) _playerBehavior.HitHazard();
        }

        enemy.Collider.CollisionEntered += HandleEnemyContact;
        enemy.Collider.CollisionStayed += HandleEnemyContact;
        Scene.AddChild(enemy);
    }

    protected void AddBoundaryWalls(double rightX = 17.2)
    {
        AddInvisibleWall(-1.2, 0, 0.3, 7);
        AddInvisibleWall(rightX, 0, 0.3, 7);
    }

    private void AddInvisibleWall(double x, double y, double width, double height)
    {
        var wall = new BoxTrigger(new Vector2D<double>(width, height), ExampleCollisionLayers.World)
        {
            Position = new Vector2D<double>(x, y)
        };
        wall.Collider.IsTrigger = false;
        wall.Collider.CollisionMask = ExampleCollisionLayers.WorldMask;
        Scene.AddChild(wall);
    }

    private void AddHud()
    {
        var panel = Scene.Gui.AddPanel();
        panel.Position = new Vector2D<float>(12, 575);
        panel.Padding = 8;
        panel.Spacing = 4;
        panel.BackgroundColor = new Vector4D<float>(0.035f, 0.055f, 0.11f, 0.88f);
        var heading = panel.AddLabel($"LEVEL {_number}/{ExampleGameLogic.LevelCount}");
        heading.Color = new Vector4D<float>(0.38f, 0.9f, 1f, 1f);
        panel.AddLabel(_title).Scale = 1.3f;
        if (HudNote is not null)
        {
            var note = panel.AddLabel(HudNote);
            note.Scale = 1.15f;
            note.Color = new Vector4D<float>(1f, 0.84f, 0.28f, 1f);
        }
        _stats = panel.AddLabel(string.Empty);
        _stats.Scale = 1.4f;
        UpdateStats();
    }

    private void UpdateStats()
    {
        if (_stats is not null) _stats.Text = $"STARS {_stars}/3   FALLS {_playerBehavior.Deaths}";
    }

    private void ConfigureCamera()
    {
        var camera = Context.MainCamera;
        camera.OrthographicSize = 2.4;
        camera.SetController(new FollowCameraController2D(_player)
        {
            TargetOffset = new Vector2D<double>(0.7, 0.35),
            SmoothTime = 0.16,
            DeadZone = new Vector2D<double>(0.65, 0.35),
            WorldBounds = CameraBounds
        });
    }
}

internal static class LevelBackground
{
    internal static LayeredBackground2D Create(int level)
    {
        var palette = LevelBackgroundPalette.For(level);
        return new LayeredBackground2D
        {
            new SkyBackgroundLayer(palette.Sky),
            new CelestialBackgroundLayer(palette.Celestial),
            new HillBackgroundLayer(palette.FarHill, parallaxFactor: 0.16, xOffset: 0.0, y: -1.45, radius: 2.0),
            new CloudBackgroundLayer(palette.Cloud),
            new HillBackgroundLayer(palette.NearHill, parallaxFactor: 0.52, xOffset: 1.4, y: -2.05, radius: 1.75)
        };
    }
}

internal abstract class LevelBackgroundLayer(double parallaxFactor) : IBackground2D
{
    public bool Enabled { get; set; } = true;

    public double ParallaxFactor { get; } = parallaxFactor;

    public Vector2D<double> ParallaxOrigin => Vector2D<double>.Zero;

    public abstract void Render(IBackgroundRenderContext2D renderer);
}

internal sealed class SkyBackgroundLayer(Vector4D<float> color) : LevelBackgroundLayer(0.0)
{
    public override void Render(IBackgroundRenderContext2D renderer)
    {
        var bounds = renderer.View;
        renderer.DrawRectangle(
            (bounds.VisibleMinimum + bounds.VisibleMaximum) * 0.5,
            bounds.VisibleMaximum - bounds.VisibleMinimum + new Vector2D<double>(0.2, 0.2),
            color);
    }
}

internal sealed class CelestialBackgroundLayer(Vector4D<float> color) : LevelBackgroundLayer(0.06)
{
    public override void Render(IBackgroundRenderContext2D renderer)
    {
        renderer.DrawCircle(new Vector2D<double>(1.2, 2.1), 0.55, color);
    }
}

internal sealed class HillBackgroundLayer(
    Vector4D<float> color,
    double parallaxFactor,
    double xOffset,
    double y,
    double radius) : LevelBackgroundLayer(parallaxFactor)
{
    public override void Render(IBackgroundRenderContext2D renderer)
    {
        for (var x = -1.0; x < 19.0; x += 3.0)
            renderer.DrawCircle(new Vector2D<double>(x + xOffset, y), radius, color);
    }
}

internal sealed class CloudBackgroundLayer(Vector4D<float> color) : LevelBackgroundLayer(0.34)
{
    public override void Render(IBackgroundRenderContext2D renderer)
    {
        DrawCloud(renderer, new Vector2D<double>(3.2, 2.05), color);
        DrawCloud(renderer, new Vector2D<double>(9.4, 2.5), color);
        DrawCloud(renderer, new Vector2D<double>(15.1, 1.85), color);
    }

    private static void DrawCloud(IRenderContext2D renderer, Vector2D<double> center, Vector4D<float> color)
    {
        renderer.DrawCircle(center + new Vector2D<double>(-0.25, 0), 0.28, color);
        renderer.DrawCircle(center + new Vector2D<double>(0.02, 0.09), 0.36, color);
        renderer.DrawCircle(center + new Vector2D<double>(0.33, 0), 0.25, color);
        renderer.DrawRectangle(center + new Vector2D<double>(0.03, -0.09), new Vector2D<double>(0.72, 0.28), color);
    }
}

internal readonly record struct LevelBackgroundPalette(
    Vector4D<float> Sky,
    Vector4D<float> Celestial,
    Vector4D<float> FarHill,
    Vector4D<float> NearHill,
    Vector4D<float> Cloud)
{
    internal static LevelBackgroundPalette For(int level)
    {
        var sky = level switch
        {
            1 => new Vector4D<float>(0.32f, 0.72f, 0.91f, 1f),
            2 => new Vector4D<float>(0.09f, 0.1f, 0.28f, 1f),
            3 => new Vector4D<float>(0.21f, 0.12f, 0.39f, 1f),
            5 => new Vector4D<float>(0.035f, 0.18f, 0.25f, 1f),
            _ => new Vector4D<float>(0.24f, 0.09f, 0.08f, 1f)
        };
        var celestial = level switch
        {
            1 => new Vector4D<float>(1f, 0.86f, 0.3f, 1f),
            3 => new Vector4D<float>(0.92f, 0.75f, 1f, 0.95f),
            4 => new Vector4D<float>(1f, 0.4f, 0.18f, 0.9f),
            5 => new Vector4D<float>(0.55f, 1f, 0.94f, 0.9f),
            _ => new Vector4D<float>(0.74f, 0.8f, 1f, 0.9f)
        };
        var farHill = level switch
        {
            1 => new Vector4D<float>(0.19f, 0.52f, 0.48f, 1f),
            2 => new Vector4D<float>(0.21f, 0.19f, 0.46f, 1f),
            3 => new Vector4D<float>(0.31f, 0.2f, 0.5f, 1f),
            5 => new Vector4D<float>(0.08f, 0.4f, 0.46f, 1f),
            _ => new Vector4D<float>(0.38f, 0.18f, 0.14f, 1f)
        };
        var nearHill = level switch
        {
            1 => new Vector4D<float>(0.14f, 0.38f, 0.37f, 1f),
            2 => new Vector4D<float>(0.14f, 0.15f, 0.36f, 1f),
            3 => new Vector4D<float>(0.18f, 0.13f, 0.36f, 1f),
            5 => new Vector4D<float>(0.04f, 0.24f, 0.31f, 1f),
            _ => new Vector4D<float>(0.2f, 0.12f, 0.13f, 1f)
        };
        var cloud = level == 1
            ? new Vector4D<float>(0.9f, 0.97f, 1f, 0.82f)
            : new Vector4D<float>(0.45f, 0.55f, 0.88f, 0.34f);
        return new LevelBackgroundPalette(sky, celestial, farHill, nearHill, cloud);
    }
}
