using Schreadt_Engine.Animation.Tweening;
using Schreadt_Engine.Component.PreFab;

namespace Example_Game.Logic;

internal static class ExampleTweens
{
    internal static void AddPanelSway(Rectangle2D panel, double rotationOffset)
    {
        var player = panel.AddComponent(new TweenPlayer());
        var sway = Tweens.To(
            () => panel.RotationRadians,
            value => panel.RotationRadians = value,
            panel.RotationRadians + rotationOffset,
            1.6);
        sway.Easing = TweenEasings.SineInOut;
        sway.LoopMode = TweenLoopMode.Yoyo;
        sway.RepeatCount = Tween.RepeatForever;
        player.Play(sway);
    }
}
