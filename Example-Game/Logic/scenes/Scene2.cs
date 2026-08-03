using Schreadt_Engine.Core;
using Silk.NET.Maths;

namespace Example_Game.Logic.scenes;

internal sealed class Scene2 : PlatformerLevelLogic
{
    internal Scene2(IInputState input)
        : base(input, 3, "LUNAR GARDENS", ExampleGameLogic.LevelFour)
    {
    }

    protected override Vector2D<double> SpawnPoint => new(0.0, -1.05);
    protected override Vector2D<double> Gravity => new(0, -6.0);
    protected override string HudNote => "LOW GRAVITY: 52%";

    protected override void BuildLevel()
    {
        AddBoundaryWalls();
        AddPlatform(1.0, -1.85, 4.0, 0.6);
        AddPlatform(4.85, -0.75, 1.4, 0.28);
        AddMovingPlatform(7.4, 0.35, 1.35, 7.0, 8.35, 0.65);
        AddPlatform(10.1, 1.15, 1.35, 0.28);
        AddPlatform(12.7, -0.15, 1.45, 0.28);
        AddPlatform(15.35, -1.85, 3.7, 0.6);

        AddSpikes(1.65, -1.55, 3);
        AddStar(4.85, -0.12);
        AddStar(10.1, 1.78);
        AddStar(12.7, 0.5);
        AddGoal(15.75, -0.95);
    }
}
