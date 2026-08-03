using Schreadt_Engine.Core;
using Silk.NET.Maths;

namespace Example_Game.Logic.scenes;

internal sealed class Scene0 : PlatformerLevelLogic
{
    internal Scene0(IInputState input) : base(input, 1, "SUNNY MEADOWS", ExampleGameLogic.LevelTwo)
    {
    }

    protected override Vector2D<double> SpawnPoint => new(0.1, -1.05);

    protected override void BuildLevel()
    {
        AddBoundaryWalls();
        AddPlatform(1.0, -1.85, 4.0, 0.6);
        AddPlatform(3.8, -1.1, 1.1, 0.28);
        AddPlatform(5.35, -0.45, 1.2, 0.28);
        AddPlatform(7.15, -1.05, 1.4, 0.28);
        AddPlatform(9.15, -1.85, 2.4, 0.6);
        AddPlatform(11.15, -0.75, 1.25, 0.28);
        AddPlatform(12.75, 0.0, 1.3, 0.28);
        AddPlatform(15.0, -1.85, 4.0, 0.6);

        AddSpikes(9.15, -1.55, 4);
        AddStar(3.8, -0.55);
        AddStar(9.15, -0.8);
        AddStar(12.75, 0.6);
        AddGoal(15.55, -0.95);
    }
}
