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
    private PlatformerPlayerLogic _playerLogic = null!;
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
    protected abstract void BuildLevel();

    public override void Init()
    {
        Scene.Background = null;
        Scene.Collisions.Gravity = Gravity;
        Scene.AddChild(new LevelBackdrop(_number));

        _playerLogic = new PlatformerPlayerLogic(_input, SpawnPoint);
        _playerLogic.StatsChanged += UpdateStats;
        _player = new PlayerAvatar(_playerLogic)
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
            if (ReferenceEquals(contact.Other.Owner, _player)) _playerLogic.HitHazard();
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
            if (!star.Active || !ReferenceEquals(contact.Other.Owner, _player)) return;
            star.Active = false;
            _stars++;
            UpdateStats();
        };
        Scene.AddChild(star);
    }

    protected void AddGoal(double x, double y)
    {
        var portal = new GoalPortal
        {
            Position = new Vector2D<double>(x, y),
            RenderLayer = 20
        };
        portal.Collider.CollisionEntered += contact =>
        {
            if (_finished || !ReferenceEquals(contact.Other.Owner, _player)) return;
            _finished = true;
            if (_nextScene is not null) State.CurrentReality.Scenes.LoadScene(_nextScene);
            else PlatformerScreens.ShowVictory(Scene, _stars, _playerLogic.Deaths);
        };
        Scene.AddChild(portal);
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
            if (ReferenceEquals(contact.Other.Owner, _player)) _playerLogic.HitHazard();
        }

        enemy.Collider.CollisionEntered += HandleEnemyContact;
        enemy.Collider.CollisionStayed += HandleEnemyContact;
        Scene.AddChild(enemy);
    }

    protected void AddBoundaryWalls()
    {
        AddInvisibleWall(-1.2, 0, 0.3, 7);
        AddInvisibleWall(17.2, 0, 0.3, 7);
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
        var heading = panel.AddLabel($"LEVEL {_number}/4");
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
        if (_stats is not null) _stats.Text = $"STARS {_stars}/3   FALLS {_playerLogic.Deaths}";
    }

    private void ConfigureCamera()
    {
        var camera = State.CurrentReality.MainCamera;
        camera.OrthographicSize = 2.4;
        camera.SetController(new FollowCameraController2D(_player)
        {
            TargetOffset = new Vector2D<double>(0.7, 0.35),
            SmoothTime = 0.16,
            DeadZone = new Vector2D<double>(0.65, 0.35),
            WorldBounds = new CameraBounds2D(new Vector2D<double>(-1, -2.8), new Vector2D<double>(17, 3.4))
        });
    }
}

internal sealed class LevelBackdrop : GameObject
{
    private readonly int _level;

    internal LevelBackdrop(int level)
    {
        _level = level;
        RenderLayer = -100;
    }

    protected override void OnRender(IRenderContext2D renderer)
    {
        var sky = _level switch
        {
            1 => new Vector4D<float>(0.32f, 0.72f, 0.91f, 1f),
            2 => new Vector4D<float>(0.09f, 0.1f, 0.28f, 1f),
            3 => new Vector4D<float>(0.21f, 0.12f, 0.39f, 1f),
            _ => new Vector4D<float>(0.24f, 0.09f, 0.08f, 1f)
        };
        var farHill = _level switch
        {
            1 => new Vector4D<float>(0.19f, 0.52f, 0.48f, 1f),
            2 => new Vector4D<float>(0.21f, 0.19f, 0.46f, 1f),
            3 => new Vector4D<float>(0.31f, 0.2f, 0.5f, 1f),
            _ => new Vector4D<float>(0.38f, 0.18f, 0.14f, 1f)
        };
        var nearHill = _level switch
        {
            1 => new Vector4D<float>(0.14f, 0.38f, 0.37f, 1f),
            2 => new Vector4D<float>(0.14f, 0.15f, 0.36f, 1f),
            3 => new Vector4D<float>(0.18f, 0.13f, 0.36f, 1f),
            _ => new Vector4D<float>(0.2f, 0.12f, 0.13f, 1f)
        };

        renderer.DrawRectangle(new Vector2D<double>(8, 0), new Vector2D<double>(24, 9), sky);
        renderer.DrawCircle(new Vector2D<double>(1.2, 2.1), 0.55, _level switch
        {
            1 => new Vector4D<float>(1f, 0.86f, 0.3f, 1f),
            3 => new Vector4D<float>(0.92f, 0.75f, 1f, 0.95f),
            4 => new Vector4D<float>(1f, 0.4f, 0.18f, 0.9f),
            _ => new Vector4D<float>(0.74f, 0.8f, 1f, 0.9f)
        });

        for (var x = -1.0; x < 19; x += 3.0)
        {
            renderer.DrawCircle(new Vector2D<double>(x, -1.45), 2.0, farHill);
            renderer.DrawCircle(new Vector2D<double>(x + 1.4, -2.05), 1.75, nearHill);
        }

        var cloud = _level == 1
            ? new Vector4D<float>(0.9f, 0.97f, 1f, 0.82f)
            : new Vector4D<float>(0.45f, 0.55f, 0.88f, 0.34f);
        DrawCloud(renderer, new Vector2D<double>(3.2, 2.05), cloud);
        DrawCloud(renderer, new Vector2D<double>(9.4, 2.5), cloud);
        DrawCloud(renderer, new Vector2D<double>(15.1, 1.85), cloud);
    }

    private static void DrawCloud(IRenderContext2D renderer, Vector2D<double> center, Vector4D<float> color)
    {
        renderer.DrawCircle(center + new Vector2D<double>(-0.25, 0), 0.28, color);
        renderer.DrawCircle(center + new Vector2D<double>(0.02, 0.09), 0.36, color);
        renderer.DrawCircle(center + new Vector2D<double>(0.33, 0), 0.25, color);
        renderer.DrawRectangle(center + new Vector2D<double>(0.03, -0.09), new Vector2D<double>(0.72, 0.28), color);
    }
}
