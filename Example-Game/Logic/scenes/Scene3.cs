using Schreadt_Engine.Core;
using Silk.NET.Maths;

namespace Example_Game.Logic.scenes;

internal sealed class Scene3 : PlatformerLevelLogic
{
    internal Scene3(IInputState input)
        : base(input, 4, "CLOCKWORK FORTRESS", ExampleGameLogic.LevelFive)
    {
    }

    protected override Vector2D<double> SpawnPoint => new(0.0, -1.05);
    protected override string HudNote => "WATCH FOR THE PATROL";

    protected override void BuildLevel()
    {
        AddBoundaryWalls();
        AddPlatform(1.0, -1.85, 4.0, 0.6);
        AddPlatform(3.75, -1.0, 1.15, 0.28);
        AddPlatform(6.25, -1.85, 3.6, 0.6);
        AddPlatform(8.85, -0.85, 1.25, 0.28);
        AddPlatform(10.5, 0.0, 1.25, 0.28);
        AddPlatform(12.15, -0.85, 1.2, 0.28);
        AddPlatform(15.0, -1.85, 4.0, 0.6);

        AddEnemy(6.0, -1.27, 4.85, 7.55, 1.15);
        AddSpikes(11.95, -0.71, 2);
        AddStar(3.75, -0.42);
        AddStar(6.25, -0.75);
        AddStar(10.5, 0.6);
        AddGoal(15.7, -0.95);
    }
}
