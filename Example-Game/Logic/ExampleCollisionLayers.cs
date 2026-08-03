using Schreadt_Engine.Collision;

namespace Example_Game.Logic;

internal static class ExampleCollisionLayers
{
    internal const int Player = 0;
    internal const int World = 1;
    internal const int Hazard = 2;
    internal const int Goal = 3;
    internal const int Collectible = 4;

    internal static CollisionLayerMask2D PlayerMask { get; } =
        CollisionLayerMask2D.FromLayers(World, Hazard, Goal, Collectible);
    internal static CollisionLayerMask2D WorldMask { get; } =
        CollisionLayerMask2D.FromLayers(Player);
    internal static CollisionLayerMask2D PlayerOnlyMask { get; } =
        CollisionLayerMask2D.FromLayers(Player);
}
