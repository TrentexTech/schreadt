using Schreadt_Engine.Animation;
using Schreadt_Engine.Component.PreFab;
using Schreadt_Engine.Core;

namespace Example_Game.Logic;

internal static class ExampleSpriteAnimations
{
    private static readonly SpriteAnimationClip BeaconPulse = new(
    [
        new SpriteAnimationFrame(TextureRegion.Full, 0.16),
        new SpriteAnimationFrame(new TextureRegion(0.015f, 0.015f, 0.985f, 0.985f), 0.16),
        new SpriteAnimationFrame(new TextureRegion(0.03f, 0.03f, 0.97f, 0.97f), 0.16)
    ], SpriteAnimationLoopMode.PingPong);

    internal static void AddBeaconPulse(Sprite sprite)
    {
        var animator = sprite.AddComponent(new SpriteAnimator { AutoPlayAnimation = "pulse" });
        animator.AddClip("pulse", BeaconPulse);
    }
}
