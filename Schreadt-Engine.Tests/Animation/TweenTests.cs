using Schreadt_Engine.Animation.Tweening;
using Schreadt_Engine.Component;
using Silk.NET.Maths;

namespace Schreadt_Engine.Tests.Animation;

public sealed class TweenTests
{
    [Fact]
    public void PropertyTween_InterpolatesAndCompletes()
    {
        var value = 2.0;
        var owner = new TestGameObject();
        var player = owner.AddComponent(new TweenPlayer());
        var started = 0;
        var completed = 0;
        var tween = Tweens.To(() => value, result => value = result, 6.0, 2.0);
        tween.Started += _ => started++;
        tween.Completed += _ => completed++;

        player.Play(tween);
        owner.Init();
        owner.Update(0.5);

        Assert.Equal(3.0, value, 10);
        Assert.Equal(1, started);
        Assert.False(tween.IsComplete);

        owner.Update(1.5);

        Assert.Equal(6.0, value, 10);
        Assert.True(tween.IsComplete);
        Assert.Equal(1, completed);
        Assert.Equal(0, player.Count);
    }

    [Fact]
    public void Delay_CapturesValueWhenTweenActuallyStarts()
    {
        var value = 1.0;
        var owner = new TestGameObject();
        var player = owner.AddComponent(new TweenPlayer());
        var tween = Tweens.To(() => value, result => value = result, 5.0, 1.0);
        tween.Delay = 0.5;
        player.Play(tween);
        owner.Init();

        owner.Update(0.25);
        value = 3.0;
        owner.Update(0.5);

        Assert.Equal(3.5, value, 10);
    }

    [Fact]
    public void YoyoRepeat_ReturnsToStartWithoutDuplicatingBoundaryTime()
    {
        var value = 0.0;
        var owner = new TestGameObject();
        var player = owner.AddComponent(new TweenPlayer());
        var cycles = 0;
        var tween = Tweens.To(() => value, result => value = result, 10.0, 1.0);
        tween.LoopMode = TweenLoopMode.Yoyo;
        tween.RepeatCount = 1;
        tween.CycleCompleted += _ => cycles++;
        player.Play(tween);
        owner.Init();

        owner.Update(1.0);
        Assert.Equal(10.0, value, 10);
        Assert.False(tween.IsComplete);

        owner.Update(1.0);
        Assert.Equal(0.0, value, 10);
        Assert.True(tween.IsComplete);
        Assert.Equal(2, cycles);
    }

    [Fact]
    public void RestartRepeat_SnapsBackAndRunsAgain()
    {
        var value = 0.0;
        var owner = new TestGameObject();
        var player = owner.AddComponent(new TweenPlayer());
        var tween = Tweens.To(() => value, result => value = result, 8.0, 1.0);
        tween.RepeatCount = 1;
        player.Play(tween);
        owner.Init();

        owner.Update(1.25);

        Assert.Equal(2.0, value, 10);
        Assert.False(tween.IsComplete);
    }

    [Fact]
    public void Sequence_CarriesUnusedTimeIntoFollowingSteps()
    {
        var value = 0.0;
        var callbackCount = 0;
        var owner = new TestGameObject();
        var player = owner.AddComponent(new TweenPlayer());
        var sequence = Tweens.Sequence(
            Tweens.To(() => value, result => value = result, 2.0, 0.5),
            Tweens.Callback(() => callbackCount++),
            Tweens.To(() => value, result => value = result, 4.0, 0.5));
        player.Play(sequence);
        owner.Init();

        owner.Update(0.75);

        Assert.Equal(3.0, value, 10);
        Assert.Equal(1, callbackCount);
        Assert.False(sequence.IsComplete);

        owner.Update(0.25);
        Assert.True(sequence.IsComplete);
        Assert.Equal(4.0, value, 10);
    }

    [Fact]
    public void Parallel_CompletesAfterLongestChild()
    {
        var first = 0.0;
        var second = 0.0;
        var owner = new TestGameObject();
        var player = owner.AddComponent(new TweenPlayer());
        var parallel = Tweens.Parallel(
            Tweens.To(() => first, result => first = result, 1.0, 0.5),
            Tweens.To(() => second, result => second = result, 2.0, 1.0));
        player.Play(parallel);
        owner.Init();

        owner.Update(0.5);
        Assert.Equal(1.0, first, 10);
        Assert.Equal(1.0, second, 10);
        Assert.False(parallel.IsComplete);

        owner.Update(0.5);
        Assert.True(parallel.IsComplete);
        Assert.Equal(2.0, second, 10);
    }

    [Fact]
    public void Player_SupportsPauseTimeScaleAndCancellation()
    {
        var value = 0.0;
        var owner = new TestGameObject();
        var player = owner.AddComponent(new TweenPlayer { TimeScale = 2.0 });
        var tween = player.Play(Tweens.To(() => value, result => value = result, 10.0, 2.0));
        owner.Init();

        Assert.True(player.Pause(tween));
        owner.Update(0.5);
        Assert.Equal(0.0, value, 10);

        Assert.True(player.Resume(tween));
        owner.Update(0.5);
        Assert.Equal(5.0, value, 10);

        Assert.True(player.Cancel(tween));
        owner.Update(1.0);
        Assert.Equal(5.0, value, 10);
    }

    [Fact]
    public void BuiltInVectorAndEasingTweensAreAvailable()
    {
        var position = new Vector2D<double>(0.0, 2.0);
        var color = new Vector4D<float>(0.0f, 0.0f, 0.0f, 0.0f);
        var positionTween = Tweens.To(
            () => position,
            value => position = value,
            new Vector2D<double>(4.0, 6.0),
            1.0);
        var colorTween = Tweens.To(
            () => color,
            value => color = value,
            Vector4D<float>.One,
            1.0);
        positionTween.Easing = TweenEasings.QuadraticIn;

        positionTween.Restart();
        colorTween.Restart();
        positionTween.Advance(0.5);
        colorTween.Advance(0.5);

        Assert.Equal(new Vector2D<double>(1.0, 3.0), position);
        Assert.Equal(new Vector4D<float>(0.5f), color);
    }

    [Fact]
    public void TweenConfiguration_RejectsInvalidValues()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Tweens.To(() => 0.0, _ => { }, 1.0, 0.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DelayTween(-1.0));

        var tween = Tweens.To(() => 0.0, _ => { }, 1.0, 1.0);
        Assert.Throws<ArgumentOutOfRangeException>(() => tween.Delay = -1.0);
        Assert.Throws<ArgumentOutOfRangeException>(() => tween.RepeatCount = -2);
        Assert.Throws<ArgumentNullException>(() => tween.Easing = null!);

        var player = new TweenPlayer();
        Assert.Throws<ArgumentOutOfRangeException>(() => player.TimeScale = -1.0);
    }

    private sealed class TestGameObject : GameObject
    {
    }
}
