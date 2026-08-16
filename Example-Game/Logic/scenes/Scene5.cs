using Schreadt_Engine.Collision;
using Schreadt_Engine.Component;
using Schreadt_Engine.Component.PreFab;
using Schreadt_Engine.Core;
using Schreadt_Engine.Gui;
using Silk.NET.Maths;

namespace Example_Game.Logic.scenes;

/// <summary>A terminal foundry route that combines the engine's 2D gameplay systems.</summary>
internal sealed class Scene5 : PlatformerLevelLogic
{
    private readonly KineticFoundryEffects _effects = new();
    private FoundryCrate _lightCrate = null!;
    private FoundryCrate _heavyCrate = null!;
    private FoundryPressurePlate _materialPlate = null!;
    private FoundryDoor _materialDoor = null!;
    private FoundrySeesaw _balanceSeesaw = null!;
    private FoundryCrate _balanceLeftCrate = null!;
    private FoundryCrate _balanceRightCrate = null!;
    private FoundryPressLinkage _pressLinkage = null!;
    private FoundryDoor _balanceDoor = null!;
    private FoundryPendulumHammer _hammer = null!;
    private FoundryLatch _hammerLatch = null!;
    private FoundryCrate _momentumBlock = null!;
    private FoundryPressurePlate _momentumPlate = null!;
    private FoundryDoor _momentumDoor = null!;
    private FoundryRotor _rotor = null!;
    private FoundryCrate _ignitionCrate = null!;
    private FoundrySeesaw _ignitionSeesaw = null!;
    private FoundryCrateSensor _ignitionSensor = null!;
    private FoundryDoor _ignitionDoor = null!;
    private GuiLabel _objective = null!;
    private bool _materialSolved;
    private bool _balanceSolved;
    private bool _momentumSolved;
    private bool _ignited;

    internal Scene5(IInputState input)
        : base(input, 6, "KINETIC FOUNDRY", null)
    {
    }

    protected override Vector2D<double> SpawnPoint => new(0.0, -1.05);
    protected override string HudNote => "E: OPERATE   BLUE STATIONS: RESET LOCAL MECHANISM";
    protected override CameraBounds2D CameraBounds => new(
        new Vector2D<double>(-1.0, -2.8),
        new Vector2D<double>(31.0, 3.4));

    protected override void BuildLevel()
    {
        AddBoundaryWalls(31.2);
        AddPlatform(15.0, -1.85, 32.0, 0.6, new Vector4D<float>(0.19f, 0.16f, 0.18f, 1.0f));
        AddPlatform(4.0, 1.55, 3.0, 0.22, new Vector4D<float>(0.23f, 0.18f, 0.2f, 1.0f));
        AddPlatform(12.3, 1.65, 2.8, 0.22, new Vector4D<float>(0.23f, 0.18f, 0.2f, 1.0f));
        AddPlatform(19.0, 1.78, 3.8, 0.22, new Vector4D<float>(0.23f, 0.18f, 0.2f, 1.0f));
        AddPlatform(25.8, 1.35, 4.0, 0.22, new Vector4D<float>(0.23f, 0.18f, 0.2f, 1.0f));

        if (Scene.Background is LayeredBackground2D background)
            background.Add(new KineticFoundryBackground());
        Scene.AddCompositionPass(_effects.Heat);
        Scene.AddCompositionPass(_effects.Sparks);
        Scene.AddCompositionPass(_effects.Flash);

        BuildMaterialBay();
        BuildBalancePress();
        BuildMomentumHall();
        BuildRotatingAssembly();
        BuildIgnitionFinale();

        AddStar(4.6, -0.8);
        AddStar(15.35, -0.8);
        AddStar(26.8, 0.2);
        AddGoal(29.55, -0.95);
        AddObjectivePanel();
    }

    public override void Update(double dt)
    {
        _effects.Update(dt);

        if (!_materialSolved && _materialPlate.Evaluate())
        {
            _materialSolved = true;
            _materialDoor.Open();
        }

        if (!_balanceSolved && IsBalancePressLoaded())
        {
            _balanceSolved = true;
            _balanceDoor.Open();
            _pressLinkage.Engage();
        }

        if (!_momentumSolved && _momentumPlate.Evaluate())
        {
            _momentumSolved = true;
            _momentumDoor.Open();
        }

        RecoverFallenObjects();
        UpdateObjective();
    }

    private void BuildMaterialBay()
    {
        _lightCrate = AddFoundryCrate(
            new Vector2D<double>(1.35, -1.28),
            mass: 0.45,
            friction: 0.08,
            new Vector4D<float>(0.24f, 0.78f, 0.94f, 1.0f));
        _heavyCrate = AddFoundryCrate(
            new Vector2D<double>(2.65, -1.23),
            mass: 2.8,
            friction: 1.35,
            new Vector4D<float>(0.72f, 0.34f, 0.16f, 1.0f),
            new Vector2D<double>(0.72, 0.56));
        _materialPlate = new FoundryPressurePlate(new Vector2D<double>(4.35, -1.5), _lightCrate);
        _materialDoor = AddDoor(5.25);
        Scene.AddChild(_materialPlate);
        AddResetStation(0.55, "MATERIAL BAY", () =>
        {
            _lightCrate.Reset();
            _heavyCrate.Reset();
        });
    }

    private void BuildBalancePress()
    {
        var anchor = new Vector2D<double>(7.85, -1.18);
        _balanceSeesaw = new FoundrySeesaw(new Vector2D<double>(3.0, 0.18), anchor);
        _balanceLeftCrate = AddFoundryCrate(
            new Vector2D<double>(6.0, -1.28),
            0.65,
            0.72,
            new Vector4D<float>(0.92f, 0.56f, 0.16f, 1.0f),
            new Vector2D<double>(0.52, 0.4));
        _balanceRightCrate = AddFoundryCrate(
            new Vector2D<double>(9.7, -1.28),
            0.65,
            0.72,
            new Vector4D<float>(0.92f, 0.56f, 0.16f, 1.0f),
            new Vector2D<double>(0.52, 0.4));
        _pressLinkage = new FoundryPressLinkage(new Vector2D<double>(7.85, 0.5));
        _balanceDoor = AddDoor(10.85);
        Scene.AddChild(_balanceSeesaw);
        Scene.AddChild(_pressLinkage);
        AddResetStation(9.85, "BALANCE PRESS", ResetBalancePress);
    }

    private void BuildMomentumHall()
    {
        var anchor = new Vector2D<double>(12.3, 0.45);
        const double startRotation = -0.82;
        var rotatedLocalAnchor = new Vector2D<double>(
            -Math.Sin(startRotation) * 0.85,
            Math.Cos(startRotation) * 0.85);
        _hammer = new FoundryPendulumHammer(
            anchor - rotatedLocalAnchor,
            anchor,
            startRotation);
        Scene.AddChild(new Rectangle2D
        {
            Position = new Vector2D<double>(anchor.X, 1.05),
            Size = new Vector2D<double>(0.12, 1.2),
            Color = new Vector4D<float>(0.4f, 0.27f, 0.22f, 1.0f),
            RenderLayer = 18
        });
        _momentumBlock = AddFoundryCrate(
            new Vector2D<double>(13.55, -1.18),
            1.15,
            0.42,
            new Vector4D<float>(0.66f, 0.28f, 0.18f, 1.0f),
            new Vector2D<double>(0.66, 0.66));
        _momentumPlate = new FoundryPressurePlate(new Vector2D<double>(14.25, -1.5), _momentumBlock);
        _hammerLatch = new FoundryLatch(_hammer.Release)
        {
            Position = new Vector2D<double>(11.3, -1.12)
        };
        _momentumDoor = AddDoor(16.1);
        Scene.AddChild(_hammer);
        Scene.AddChild(_hammerLatch);
        Scene.AddChild(_momentumPlate);
        AddResetStation(15.55, "HAMMER", ResetMomentumHall);
    }

    private void BuildRotatingAssembly()
    {
        _rotor = new FoundryRotor(new Vector2D<double>(19.0, -0.02));
        var safety = new FoundrySafetySensor(new Vector2D<double>(19.0, 1.58), _rotor);
        Scene.AddChild(_rotor);
        Scene.AddChild(safety);
        AddPlatform(17.0, -1.0, 1.2, 0.18, new Vector4D<float>(0.28f, 0.22f, 0.3f, 1.0f));
        AddPlatform(21.0, -0.65, 1.2, 0.18, new Vector4D<float>(0.28f, 0.22f, 0.3f, 1.0f));
    }

    private void BuildIgnitionFinale()
    {
        _ignitionCrate = AddFoundryCrate(
            new Vector2D<double>(21.2, -1.18),
            0.9,
            0.68,
            new Vector4D<float>(0.84f, 0.46f, 0.12f, 1.0f));
        _ignitionSeesaw = new FoundrySeesaw(
            new Vector2D<double>(2.0, 0.16),
            new Vector2D<double>(25.0, -1.18));
        Scene.AddChild(new FoundryRamp(
            new Vector2D<double>(21.8, -1.55),
            new Vector2D<double>(24.0, -1.10)));
        _ignitionSensor = new FoundryCrateSensor(new Vector2D<double>(24.6, -0.84), _ignitionCrate);
        _ignitionDoor = AddDoor(28.05);
        var ignitionLever = new FoundryIgnitionLever(
            () => _ignitionSensor.BlockedByCrate,
            IgniteFoundry)
        {
            Position = new Vector2D<double>(27.15, -1.1)
        };
        Scene.AddChild(_ignitionSeesaw);
        Scene.AddChild(_ignitionSensor);
        Scene.AddChild(ignitionLever);
        AddResetStation(20.9, "IGNITION RIG", () =>
        {
            _ignitionCrate.Reset();
            _ignitionSeesaw.Reset();
        });
    }

    private FoundryCrate AddFoundryCrate(
        Vector2D<double> position,
        double mass,
        double friction,
        Vector4D<float> color,
        Vector2D<double>? size = null)
    {
        var crate = new FoundryCrate(position, mass, friction, color, size);
        Scene.AddChild(crate);
        return crate;
    }

    private FoundryDoor AddDoor(double x)
    {
        var door = new FoundryDoor(new Vector2D<double>(x, -0.25));
        Scene.AddChild(door);
        return door;
    }

    private void AddResetStation(double x, string mechanism, Action reset)
    {
        Scene.AddChild(new FoundryResetStation(mechanism, () =>
        {
            PlayerInteraction.ClearFocus();
            reset();
        })
        {
            Position = new Vector2D<double>(x, -1.12)
        });
    }

    private bool IsBalancePressLoaded()
    {
        var leftOffset = _balanceLeftCrate.Position - _balanceSeesaw.Position;
        var rightOffset = _balanceRightCrate.Position - _balanceSeesaw.Position;
        return leftOffset.X is >= -1.45 and <= -0.35 &&
               rightOffset.X is >= 0.35 and <= 1.45 &&
               leftOffset.Y is >= 0.15 and <= 0.75 &&
               rightOffset.Y is >= 0.15 and <= 0.75 &&
               Math.Abs(_balanceSeesaw.RotationRadians) < 0.16;
    }

    private void ResetBalancePress()
    {
        _balanceSeesaw.Reset();
        _balanceLeftCrate.Reset();
        _balanceRightCrate.Reset();
    }

    private void ResetMomentumHall()
    {
        _hammer.Reset();
        _hammerLatch.Reset();
        _momentumBlock.Reset();
    }

    private void IgniteFoundry()
    {
        if (_ignited || !_ignitionSensor.BlockedByCrate) return;

        // Gameplay state and collision change first; presentation cannot decide success.
        _ignited = true;
        _ignitionDoor.Open();
        _effects.TriggerIgnition();
        var camera = Context.MainCamera;
        var shake = camera.GetComponent<CameraShake2D>() ?? camera.AddComponent(new CameraShake2D());
        shake.Shake(0.55, 0.1, 0.016, 28.0);
    }

    private void RecoverFallenObjects()
    {
        Recover(_lightCrate);
        Recover(_heavyCrate);
        Recover(_balanceLeftCrate);
        Recover(_balanceRightCrate);
        Recover(_momentumBlock);
        Recover(_ignitionCrate);
        if (_hammer.Position.Y < -3.0) ResetMomentumHall();
    }

    private static void Recover(FoundryCrate crate)
    {
        if (crate.Position.Y < -3.0) crate.Reset();
    }

    private void AddObjectivePanel()
    {
        var panel = Scene.Gui.AddPanel();
        panel.Position = new Vector2D<float>(455, 12);
        panel.Padding = 7;
        panel.BackgroundColor = new Vector4D<float>(0.035f, 0.045f, 0.065f, 0.9f);
        var heading = panel.AddLabel("FOUNDRY OBJECTIVE");
        heading.Color = new Vector4D<float>(1.0f, 0.48f, 0.12f, 1.0f);
        _objective = panel.AddLabel("COMPARE THE MATERIAL CRATES");
        _objective.Scale = 1.2f;
    }

    private void UpdateObjective()
    {
        _objective.Text = !_materialSolved
            ? "LOW MASS + LOW FRICTION -> PLATE"
            : !_balanceSolved
                ? "LOAD BOTH SIDES OF THE LIMITED SEESAW"
                : !_momentumSolved
                    ? "RELEASE HAMMER -> DRIVE BLOCK INTO CRADLE"
                    : Player.Position.X < 22.0
                        ? "CROSS THE ROTOR; SENSOR STOPS IT"
                        : !_ignited
                            ? "BLOCK BEAM WITH CRATE, THEN PULL LEVER"
                            : "FOUNDRY LIT - ENTER THE PORTAL";
    }
}
