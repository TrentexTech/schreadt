using Schreadt_Engine.Collision;

namespace Example_Game.Logic;

internal static class ExampleCollisionLayers
{
    internal const int Player = 0;
    internal const int World = 1;
    internal const int Hazard = 2;
    internal const int Trigger = 3;

    internal static CollisionLayerMask2D PlayerMask { get; } =
        CollisionLayerMask2D.FromLayers(World, Hazard, Trigger);

    internal static CollisionLayerMask2D WorldMask { get; } =
        CollisionLayerMask2D.FromLayers(Player, Hazard);

    internal static CollisionLayerMask2D HazardMask { get; } =
        CollisionLayerMask2D.FromLayers(Player, World);

    internal static CollisionLayerMask2D TriggerMask { get; } =
        CollisionLayerMask2D.FromLayers(Player);
}
