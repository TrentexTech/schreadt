using Example_Game.Logic;
using Schreadt_Engine.Animation.Tweening;

namespace Schreadt_Engine.Tests.Examples;

public sealed class PlatformerTweenTests
{
    [Fact]
    public void StarCollectionTween_DisablesCollisionAndDeactivatesAfterAnimation()
    {
        var star = new StarToken();
        star.Init();

        Assert.True(star.Collect());
        Assert.False(star.Collider.Enabled);
        Assert.True(star.Active);
        Assert.Single(star.GetComponent<TweenPlayer>()!.ActiveTweens);

        star.Update(0.3);

        Assert.False(star.Active);
        Assert.False(star.Collect());
    }
}
