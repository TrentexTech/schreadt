using Schreadt_Engine.Animation.Tweening;
using Schreadt_Engine.Collision;
using Schreadt_Engine.Component;
using Schreadt_Engine.Component.Logic;
using Schreadt_Engine.Component.PreFab;
using Schreadt_Engine.Core;
using Silk.NET.Maths;

namespace Example_Game.Logic;

internal sealed class PlatformerPlayerLogic : ActorLogic
{
    private const double RunSpeed = 3.15;
    private const double GroundAcceleration = 22.0;
    private const double AirAcceleration = 11.0;
    private const double JumpSpeed = 5.35;
    private const double CoyoteTime = 0.12;
    private const double JumpBufferTime = 0.14;
    private readonly IInputState _input;
    private readonly Vector2D<double> _spawn;
    private readonly HashSet<Collider2D> _groundContacts = [];
    private RigidBody2D _body = null!;
    private CircleCollider2D _collider = null!;
    private double _coyoteTimer;
    private double _jumpBufferTimer;
    private double _respawnGrace;

    internal int Deaths { get; private set; }
    internal event Action? StatsChanged;

    internal PlatformerPlayerLogic(IInputState input, Vector2D<double> spawn)
    {
        _input = input;
        _spawn = spawn;
    }

    public override void Init()
    {
        _body = Actor.GetComponent<RigidBody2D>()
            ?? throw new InvalidOperationException("The platformer player needs a rigid body.");
        _collider = Actor.GetComponent<CircleCollider2D>()
            ?? throw new InvalidOperationException("The platformer player needs a circle collider.");
        _collider.CollisionEntered += TrackGroundContact;
        _collider.CollisionStayed += TrackGroundContact;
        _collider.CollisionExited += contact => _groundContacts.Remove(contact.Other);
    }

    public override void Update(double dt)
    {
        _respawnGrace = Math.Max(0, _respawnGrace - dt);
        _coyoteTimer = _groundContacts.Count > 0 ? CoyoteTime : Math.Max(0, _coyoteTimer - dt);
        _jumpBufferTimer = Math.Max(0, _jumpBufferTimer - dt);

        // Refreshing the buffer while jump is held makes landing immediately
        // start the next jump without losing coyote time or short-hop control.
        if (_input.IsActionDown(ExampleInputActions.Jump)) _jumpBufferTimer = JumpBufferTime;

        var direction = 0.0;
        if (_input.IsActionDown(ExampleInputActions.MoveLeft)) direction -= 1.0;
        if (_input.IsActionDown(ExampleInputActions.MoveRight)) direction += 1.0;

        var velocity = _body.Velocity;
        var acceleration = _groundContacts.Count > 0 ? GroundAcceleration : AirAcceleration;
        velocity.X = MoveTowards(velocity.X, direction * RunSpeed, acceleration * dt);

        if (_jumpBufferTimer > 0 && _coyoteTimer > 0)
        {
            velocity.Y = JumpSpeed;
            _jumpBufferTimer = 0;
            _coyoteTimer = 0;
            _groundContacts.Clear();
        }

        // Releasing jump early gives the player a short, controllable hop.
        if (_input.WasActionReleased(ExampleInputActions.Jump) && velocity.Y > 1.3)
            velocity.Y *= 0.48;

        _body.Velocity = velocity;
        if (Actor.Position.Y < -3.4) Respawn();
    }

    internal void HitHazard()
    {
        if (_respawnGrace <= 0) Respawn();
    }

    private void Respawn()
    {
        Deaths++;
        _respawnGrace = 0.65;
        _groundContacts.Clear();
        _coyoteTimer = 0;
        Actor.Position = _spawn;
        _body.Velocity = Vector2D<double>.Zero;
        StatsChanged?.Invoke();

        var camera = State.CurrentReality.MainCamera;
        var shake = camera.GetComponent<CameraShake2D>() ?? camera.AddComponent(new CameraShake2D());
        shake.Shake(0.22, 0.07, 0.012);
    }

    private void TrackGroundContact(CollisionContact2D contact)
    {
        if (contact.Other.CollisionLayer != ExampleCollisionLayers.World) return;
        if (contact.Normal.Y < -0.55) _groundContacts.Add(contact.Other);
        else _groundContacts.Remove(contact.Other);
    }

    private static double MoveTowards(double value, double target, double amount)
    {
        if (Math.Abs(target - value) <= amount) return target;
        return value + Math.CopySign(amount, target - value);
    }
}

internal sealed class PlayerAvatar : Actor
{
    internal const double PlayerRadius = 0.23;

    internal PlayerAvatar(PlatformerPlayerLogic logic) : base(logic)
    {
        AddComponent(new RigidBody2D
        {
            BodyType = CollisionBodyType2D.Dynamic,
            Mass = 1,
            Restitution = 0,
            Friction = 0.18,
            LinearDamping = 0.05,
            MaximumSpeed = 7,
            AllowSleep = false
        });
        AddComponent(new CircleCollider2D(PlayerRadius)
        {
            CollisionLayer = ExampleCollisionLayers.Player,
            CollisionMask = ExampleCollisionLayers.PlayerMask
        });
    }

    protected override void OnRender(IRenderContext2D renderer)
    {
        renderer.DrawCircle(Position + new Vector2D<double>(0, -0.025), PlayerRadius * 1.05,
            new Vector4D<float>(0.04f, 0.12f, 0.24f, 0.28f));
        renderer.DrawCircle(Position, PlayerRadius, new Vector4D<float>(1f, 0.39f, 0.28f, 1f));
        renderer.DrawCircle(Position + new Vector2D<double>(-0.075, 0.055), 0.055,
            new Vector4D<float>(1f, 0.95f, 0.78f, 1f));
        renderer.DrawCircle(Position + new Vector2D<double>(0.075, 0.055), 0.055,
            new Vector4D<float>(1f, 0.95f, 0.78f, 1f));
        renderer.DrawCircle(Position + new Vector2D<double>(-0.062, 0.058), 0.021,
            new Vector4D<float>(0.04f, 0.08f, 0.15f, 1f));
        renderer.DrawCircle(Position + new Vector2D<double>(0.062, 0.058), 0.021,
            new Vector4D<float>(0.04f, 0.08f, 0.15f, 1f));
        renderer.DrawRectangle(Position + new Vector2D<double>(0, -0.105), new Vector2D<double>(0.13, 0.035),
            new Vector4D<float>(0.35f, 0.08f, 0.16f, 1f));
    }
}

internal sealed class BoxTrigger : GameObject
{
    internal AxisAlignedBoxCollider2D Collider { get; }

    internal BoxTrigger(Vector2D<double> size, int layer)
    {
        Collider = AddComponent(new AxisAlignedBoxCollider2D(size)
        {
            IsTrigger = true,
            CollisionLayer = layer,
            CollisionMask = ExampleCollisionLayers.PlayerOnlyMask
        });
    }
}

internal sealed class StarToken : Actor
{
    // The renderer accepts convex polygons only. A star is therefore composed
    // from five convex rays instead of one concave outline.
    private static readonly Vector2D<double>[] StarRay =
    [
        new(0, 0.27), new(-0.085, 0.045), new(0.085, 0.045)
    ];
    private readonly TweenPlayer _tweens;
    private Vector2D<double> _visualScale = Vector2D<double>.One;
    private double _rotation;
    private float _opacity = 1.0f;
    private bool _collected;

    internal CircleCollider2D Collider { get; }

    internal StarToken()
    {
        Collider = AddComponent(new CircleCollider2D(0.2)
        {
            IsTrigger = true,
            CollisionLayer = ExampleCollisionLayers.Collectible,
            CollisionMask = ExampleCollisionLayers.PlayerOnlyMask
        });
        _tweens = AddComponent(new TweenPlayer());

        var pulse = Tweens.To(
            () => _visualScale,
            value => _visualScale = value,
            new Vector2D<double>(1.12, 1.12),
            0.6);
        pulse.Easing = TweenEasings.SineInOut;
        pulse.LoopMode = TweenLoopMode.Yoyo;
        pulse.RepeatCount = Tween.RepeatForever;
        _tweens.Play(pulse);

        var spin = Tweens.To(() => _rotation, value => _rotation = value, Math.Tau, 6.0);
        spin.RepeatCount = Tween.RepeatForever;
        _tweens.Play(spin);
    }

    internal bool Collect()
    {
        if (_collected) return false;

        _collected = true;
        Collider.Enabled = false;
        _tweens.Clear();

        var rise = Tweens.To(
            () => Position,
            value => Position = value,
            Position + new Vector2D<double>(0.0, 0.38),
            0.3);
        rise.Easing = TweenEasings.CubicOut;

        var shrink = Tweens.To(
            () => _visualScale,
            value => _visualScale = value,
            new Vector2D<double>(0.12, 0.12),
            0.3);
        shrink.Easing = TweenEasings.QuadraticIn;

        var fade = Tweens.To(() => _opacity, value => _opacity = value, 0.0f, 0.3);
        fade.Easing = TweenEasings.QuadraticIn;

        _tweens.Play(Tweens.Sequence(
            Tweens.Parallel(rise, shrink, fade),
            Tweens.Callback(() => Active = false)));
        return true;
    }

    protected override void OnRender(IRenderContext2D renderer)
    {
        var scale = _visualScale.X;
        var color = new Vector4D<float>(1f, 0.84f, 0.18f, _opacity);
        renderer.DrawCircle(Position, 0.28 * scale, new Vector4D<float>(1f, 0.78f, 0.1f, 0.18f * _opacity));
        for (var ray = 0; ray < 5; ray++)
        {
            renderer.DrawPolygon(Position, StarRay, _visualScale,
                _rotation + ray * Math.PI * 2.0 / 5.0, color);
        }
        renderer.DrawCircle(Position, 0.105 * scale, color);
    }
}

internal sealed class GoalPortal : Actor
{
    private double _pulse;
    private double _orbitAngle;
    internal CircleCollider2D Collider { get; }

    internal GoalPortal()
    {
        Collider = AddComponent(new CircleCollider2D(0.42)
        {
            IsTrigger = true,
            CollisionLayer = ExampleCollisionLayers.Goal,
            CollisionMask = ExampleCollisionLayers.PlayerOnlyMask
        });
        var tweens = AddComponent(new TweenPlayer());

        var pulse = Tweens.To(() => _pulse, value => _pulse = value, 0.025, 0.55);
        pulse.Easing = TweenEasings.SineInOut;
        pulse.LoopMode = TweenLoopMode.Yoyo;
        pulse.RepeatCount = Tween.RepeatForever;
        tweens.Play(pulse);

        var orbit = Tweens.To(() => _orbitAngle, value => _orbitAngle = value, Math.Tau, 3.0);
        orbit.RepeatCount = Tween.RepeatForever;
        tweens.Play(orbit);
    }

    protected override void OnRender(IRenderContext2D renderer)
    {
        renderer.DrawCircle(Position, 0.5 + _pulse, new Vector4D<float>(0.35f, 0.95f, 1f, 0.2f));
        renderer.DrawCircle(Position, 0.39 + _pulse, new Vector4D<float>(0.26f, 0.82f, 1f, 0.8f));
        renderer.DrawCircle(Position, 0.27, new Vector4D<float>(0.06f, 0.12f, 0.3f, 1f));
        renderer.DrawCircle(Position + new Vector2D<double>(0.08 * Math.Sin(_orbitAngle), 0.08 * Math.Cos(_orbitAngle)),
            0.055, new Vector4D<float>(1f, 0.9f, 0.35f, 1f));
    }
}

internal sealed class MovingPlatform : Rectangle2D
{
    private readonly double _minimumX;
    private readonly double _maximumX;
    private readonly double _speed;
    private readonly RigidBody2D _body;

    internal MovingPlatform(Vector2D<double> size, double minimumX, double maximumX, double speed)
    {
        Size = size;
        _minimumX = minimumX;
        _maximumX = maximumX;
        _speed = speed;
        _body = AddComponent(new RigidBody2D { BodyType = CollisionBodyType2D.Kinematic });
        _body.Velocity = new Vector2D<double>(_speed, 0);
        AddComponent(new AxisAlignedBoxCollider2D(size)
        {
            CollisionLayer = ExampleCollisionLayers.World,
            CollisionMask = ExampleCollisionLayers.WorldMask
        });
    }

    protected override void OnUpdate(double dt)
    {
        base.OnUpdate(dt);
        if (Position.X <= _minimumX) _body.Velocity = new Vector2D<double>(_speed, 0);
        else if (Position.X >= _maximumX) _body.Velocity = new Vector2D<double>(-_speed, 0);
    }
}

internal sealed class PatrolEnemy : Actor
{
    private readonly double _minimumX;
    private readonly double _maximumX;
    private readonly double _speed;
    private readonly RigidBody2D _body;
    private double _time;

    internal CircleCollider2D Collider { get; }

    internal PatrolEnemy(double minimumX, double maximumX, double speed)
    {
        if (minimumX >= maximumX) throw new ArgumentException("Enemy patrol bounds must have positive width.");
        if (speed <= 0) throw new ArgumentOutOfRangeException(nameof(speed));

        _minimumX = minimumX;
        _maximumX = maximumX;
        _speed = speed;
        _body = AddComponent(new RigidBody2D { BodyType = CollisionBodyType2D.Kinematic });
        _body.Velocity = new Vector2D<double>(_speed, 0);
        Collider = AddComponent(new CircleCollider2D(0.27)
        {
            IsTrigger = true,
            CollisionLayer = ExampleCollisionLayers.Hazard,
            CollisionMask = ExampleCollisionLayers.PlayerOnlyMask
        });
    }

    protected override void OnUpdate(double dt)
    {
        base.OnUpdate(dt);
        _time += dt;
        if (Position.X <= _minimumX) _body.Velocity = new Vector2D<double>(_speed, 0);
        else if (Position.X >= _maximumX) _body.Velocity = new Vector2D<double>(-_speed, 0);
    }

    protected override void OnRender(IRenderContext2D renderer)
    {
        var bob = Math.Sin(_time * 7.0) * 0.018;
        var center = Position + new Vector2D<double>(0, bob);
        var shell = new Vector4D<float>(0.83f, 0.18f, 0.2f, 1f);
        var dark = new Vector4D<float>(0.18f, 0.045f, 0.065f, 1f);

        renderer.DrawRectangle(center + new Vector2D<double>(-0.22, -0.18),
            new Vector2D<double>(0.18, 0.07), dark, -0.45);
        renderer.DrawRectangle(center + new Vector2D<double>(0.22, -0.18),
            new Vector2D<double>(0.18, 0.07), dark, 0.45);
        renderer.DrawCircle(center, 0.27, dark);
        renderer.DrawCircle(center + new Vector2D<double>(0, 0.035), 0.235, shell);
        renderer.DrawRectangle(center + new Vector2D<double>(0, -0.06),
            new Vector2D<double>(0.32, 0.08), new Vector4D<float>(1f, 0.46f, 0.2f, 1f));
        renderer.DrawCircle(center + new Vector2D<double>(-0.085, 0.09), 0.052,
            new Vector4D<float>(1f, 0.9f, 0.62f, 1f));
        renderer.DrawCircle(center + new Vector2D<double>(0.085, 0.09), 0.052,
            new Vector4D<float>(1f, 0.9f, 0.62f, 1f));
        renderer.DrawCircle(center + new Vector2D<double>(-0.075, 0.09), 0.022, dark);
        renderer.DrawCircle(center + new Vector2D<double>(0.075, 0.09), 0.022, dark);
    }
}
