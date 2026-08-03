using Schreadt_Engine.Core;

namespace Schreadt_Engine.Tests.Core;

public sealed class RuntimeControllerTests
{
    [Fact]
    public void Pause_StopsSimulationButStillTracksRuntimeFrames()
    {
        var runtime = new RuntimeController();
        bool? reportedPauseState = null;
        runtime.PauseStateChanged += paused => reportedPauseState = paused;

        runtime.Pause();
        var timing = runtime.Advance(0.1);

        Assert.True(runtime.IsPaused);
        Assert.True(reportedPauseState);
        Assert.False(timing.ShouldUpdateSimulation);
        Assert.Equal(0.0, timing.FrameDeltaTime);
        Assert.Equal(0, timing.FixedStepCount);
        Assert.Equal(0.0, runtime.DeltaTime);
        Assert.Equal(0.1, runtime.UnscaledDeltaTime, 10);
        Assert.Equal(1UL, runtime.FrameCount);
    }

    [Fact]
    public void StepOneFrame_AdvancesExactlyOneFixedTickWhilePaused()
    {
        var runtime = new RuntimeController();
        runtime.Pause();
        runtime.StepOneFrame();

        var stepTiming = runtime.Advance(1.0);
        var nextTiming = runtime.Advance(1.0);

        Assert.True(stepTiming.ShouldUpdateSimulation);
        Assert.Equal(FixedStepClock.FixedDeltaTime, stepTiming.FrameDeltaTime, 12);
        Assert.Equal(1, stepTiming.FixedStepCount);
        Assert.False(runtime.IsSingleStepPending);
        Assert.False(nextTiming.ShouldUpdateSimulation);
    }

    [Fact]
    public void TimeScale_ChangesSimulationDelta()
    {
        var runtime = new RuntimeController { TimeScale = 0.5 };

        var timing = runtime.Advance(0.02);

        Assert.Equal(0.01, timing.FrameDeltaTime, 10);
        Assert.Equal(0.01, runtime.DeltaTime, 10);
        Assert.Equal(0.02, runtime.UnscaledDeltaTime, 10);
    }

    [Fact]
    public void RuntimeControls_RejectInvalidOperations()
    {
        var runtime = new RuntimeController();

        Assert.Throws<ArgumentOutOfRangeException>(() => runtime.TimeScale = 0.0);
        Assert.Throws<ArgumentOutOfRangeException>(() => runtime.TimeScale = double.NaN);
        Assert.Throws<InvalidOperationException>(() => runtime.StepOneFrame());
    }
}
