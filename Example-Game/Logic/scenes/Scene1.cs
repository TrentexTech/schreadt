using Schreadt_Engine.Core;
using Silk.NET.Maths;

namespace Example_Game.Logic.scenes;

internal sealed class Scene1 : PlatformerLevelLogic
{
    internal Scene1(IInputState input) : base(input, 2, "CRYSTAL HEIGHTS", ExampleGameLogic.LevelThree)
    {
    }

    protected override Vector2D<double> SpawnPoint => new(0.0, -1.05);

    protected override void BuildLevel()
    {
        AddBoundaryWalls();
        AddPlatform(1.0, -1.85, 4.0, 0.6);
        AddPlatform(3.8, -0.9, 1.15, 0.28);
        AddPlatform(5.35, -0.1, 1.2, 0.28);
        AddPlatform(7.05, 0.7, 1.15, 0.28);
        AddMovingPlatform(8.55, 0.0, 1.25, 8.15, 9.55, 0.75);
        AddPlatform(10.65, 0.75, 1.25, 0.28);
        AddPlatform(12.25, -0.15, 1.25, 0.28);
        AddPlatform(14.9, -1.85, 4.2, 0.6);

        AddSpikes(1.7, -1.55, 3);
        AddSpikes(12.25, -0.01, 3);
        AddStar(4.0, -0.3);
        AddStar(7.05, 1.3);
        AddStar(10.65, 1.35);
        AddGoal(15.65, -0.95);
    }
}
