using Schreadt_Engine.Animation;
using Schreadt_Engine.Component;
using Schreadt_Engine.Component.PreFab;
using Schreadt_Engine.Core;

namespace Schreadt_Engine.Tests.Animation;

public sealed class SpriteAnimationTests
{
    private static readonly TextureRegion First = new(0.0f, 0.0f, 0.25f, 1.0f);
    private static readonly TextureRegion Second = new(0.25f, 0.0f, 0.5f, 1.0f);
    private static readonly TextureRegion Third = new(0.5f, 0.0f, 0.75f, 1.0f);

    [Fact]
    public void GridClip_CreatesRegionsInRowMajorOrder()
    {
        var clip = SpriteAnimationClip.FromGrid(4, 2, [1, 6], 0.2);

        Assert.Equal(new TextureRegion(0.25f, 0.0f, 0.5f, 0.5f), clip.Frames[0].Region);
        Assert.Equal(new TextureRegion(0.5f, 0.5f, 0.75f, 1.0f), clip.Frames[1].Region);
        Assert.Equal(0.4, clip.Duration, 10);
    }

    [Fact]
    public void LoopingAnimation_AdvancesFramesAndReportsLoop()
    {
        var (sprite, animator) = CreateAnimator(SpriteAnimationLoopMode.Loop);
        var changedFrames = new List<int>();
        var loopCount = 0;
        animator.FrameChanged += change => changedFrames.Add(change.FrameIndex);
        animator.Looped += _ => loopCount++;

        animator.Play("test");
        sprite.Init();
        sprite.Update(0.31);

        Assert.Equal(0, animator.CurrentFrameIndex);
        Assert.Equal(First, sprite.Region);
        Assert.Equal([0, 1, 2, 0], changedFrames);
        Assert.Equal(1, loopCount);
    }

    [Fact]
    public void OnceAnimation_StopsAndReportsCompletionAfterLastFrameDuration()
    {
        var (sprite, animator) = CreateAnimator(SpriteAnimationLoopMode.Once);
        var completionCount = 0;
        animator.Completed += _ => completionCount++;
        animator.Play("test");
        sprite.Init();

        sprite.Update(0.31);

        Assert.False(animator.IsPlaying);
        Assert.Equal(2, animator.CurrentFrameIndex);
        Assert.Equal(Third, sprite.Region);
        Assert.Equal(1, completionCount);
    }

    [Fact]
    public void PingPongAnimation_ReversesWithoutDuplicatingEndFrames()
    {
        var (sprite, animator) = CreateAnimator(SpriteAnimationLoopMode.PingPong);
        var visitedFrames = new List<int>();
        var loopCount = 0;
        animator.FrameChanged += change => visitedFrames.Add(change.FrameIndex);
        animator.Looped += _ => loopCount++;
        animator.Play("test");
        sprite.Init();

        for (var index = 0; index < 4; index++) sprite.Update(0.11);

        Assert.Equal([0, 1, 2, 1, 0], visitedFrames);
        Assert.Equal(0, animator.CurrentFrameIndex);
        Assert.Equal(1, loopCount);
    }

    [Fact]
    public void PauseResumeAndSpeed_ControlPlayback()
    {
        var (sprite, animator) = CreateAnimator(SpriteAnimationLoopMode.Loop);
        animator.Play("test");
        sprite.Init();
        animator.Pause();

        sprite.Update(1.0);
        Assert.Equal(0, animator.CurrentFrameIndex);

        animator.Speed = 2.0;
        animator.Resume();
        sprite.Update(0.06);
        Assert.Equal(1, animator.CurrentFrameIndex);
    }

    [Fact]
    public void AutoPlay_StartsWhenOwnerInitializes()
    {
        var sprite = new Sprite("test");
        var animator = sprite.AddComponent(new SpriteAnimator { AutoPlayAnimation = "idle" });
        animator.AddClip("idle", CreateClip(SpriteAnimationLoopMode.Loop));

        sprite.Init();

        Assert.True(animator.IsPlaying);
        Assert.Equal("idle", animator.CurrentAnimationName);
        Assert.Equal(First, sprite.Region);
    }

    [Fact]
    public void Animator_RequiresSpriteRegionTargetAndRestoresRegionWhenDetached()
    {
        var ordinaryObject = new TestGameObject();
        Assert.Throws<InvalidOperationException>(() => ordinaryObject.AddComponent(new SpriteAnimator()));

        var initialRegion = new TextureRegion(0.1f, 0.1f, 0.9f, 0.9f);
        var sprite = new Sprite("test") { Region = initialRegion };
        var animator = sprite.AddComponent(new SpriteAnimator());
        animator.AddClip("test", CreateClip(SpriteAnimationLoopMode.Loop));
        animator.Play("test");

        Assert.True(sprite.RemoveComponent(animator));
        Assert.Equal(initialRegion, sprite.Region);
    }

    [Fact]
    public void AnimationDefinitions_RejectInvalidValues()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SpriteAnimationFrame(TextureRegion.Full, 0.0));
        Assert.Throws<ArgumentException>(() => new SpriteAnimationClip([]));
        Assert.Throws<ArgumentOutOfRangeException>(() => TextureRegion.FromGridCell(2, 0, 2, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => SpriteAnimationClip.FromGrid(2, 2, [4], 0.1));
    }

    private static (Sprite Sprite, SpriteAnimator Animator) CreateAnimator(SpriteAnimationLoopMode loopMode)
    {
        var sprite = new Sprite("test");
        var animator = sprite.AddComponent(new SpriteAnimator());
        animator.AddClip("test", CreateClip(loopMode));
        return (sprite, animator);
    }

    private static SpriteAnimationClip CreateClip(SpriteAnimationLoopMode loopMode)
    {
        return new SpriteAnimationClip(
        [
            new SpriteAnimationFrame(First, 0.1),
            new SpriteAnimationFrame(Second, 0.1),
            new SpriteAnimationFrame(Third, 0.1)
        ], loopMode);
    }

    private sealed class TestGameObject : GameObject
    {
    }
}
