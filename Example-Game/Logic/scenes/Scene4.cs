using Schreadt_Engine.Component;
using Schreadt_Engine.Core;
using Silk.NET.Maths;

namespace Example_Game.Logic.scenes;

internal sealed class Scene4 : PlatformerLevelLogic
{
    private TempestStorm2D _storm = null!;

    internal Scene4(IInputState input)
        : base(input, 5, "TEMPEST SPIRE", null)
    {
    }

    protected override Vector2D<double> SpawnPoint => new(0.0, -1.05);
    protected override string HudNote => "CLIMB UP: PUSH THE CRATE ONTO THE PLATE";

    protected override void BuildLevel()
    {
        _storm = new TempestStorm2D();
        if (Scene.Background is LayeredBackground2D background)
            background.Insert(4, _storm.Clouds);
        Scene.AddCompositionPass(_storm.Lightning);
        Scene.AddCompositionPass(_storm.Rain);
        Scene.AddCompositionPass(_storm.ScreenFlash);

        AddBoundaryWalls();
        AddPlatform(1.0, -1.85, 4.0, 0.6);
        AddUpdraft(3.45, -0.75, 0.95);
        AddPlatform(5.0, 0.15, 1.4, 0.28);
        AddPlatform(6.75, -0.72, 1.35, 0.28);
        AddPlatform(8.3, -1.85, 3.0, 0.6);
        AddPlatform(7.8, 0.35, 2.6, 0.28);
        var laser = AddLaserScanner(10.15, 2.25, -1.78, -1.36, 1.35);
        AddCratePressurePlate(laser, 7.1, 0.74, 8.35, 0.55);
        AddPlatform(10.9, -0.82, 1.25, 0.28);
        AddUpdraft(12.15, -0.42, 1.05);
        AddPlatform(13.5, 0.82, 1.35, 0.28);
        AddPlatform(15.55, -1.85, 3.1, 0.6);

        AddStar(5.0, 0.78);
        AddStar(10.9, -0.2);
        AddStar(13.5, 1.45);
        AddGoal(16.0, -0.95);
    }

    public override void Update(double dt)
    {
        _storm.Update(dt);
    }
}
