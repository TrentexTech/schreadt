using Schreadt_Engine.Core;
using Silk.NET.SDL;
using EngineWindow = Schreadt_Engine.Core.Window;

namespace Schreadt_Engine.Tests.Core;

public sealed class WindowFrameDispatcherTests
{
    [Theory]
    [InlineData(WindowEventID.Moved)]
    [InlineData(WindowEventID.Resized)]
    [InlineData(WindowEventID.SizeChanged)]
    [InlineData(WindowEventID.Exposed)]
    public void LiveInteractionEvents_RequestAnImmediateFrame(WindowEventID eventId)
    {
        Assert.True(EngineWindow.IsLiveInteractionEvent(eventId));
    }

    [Theory]
    [InlineData(WindowEventID.FocusGained)]
    [InlineData(WindowEventID.FocusLost)]
    [InlineData(WindowEventID.Minimized)]
    [InlineData(WindowEventID.Close)]
    public void OtherWindowEvents_DoNotRequestAnImmediateFrame(WindowEventID eventId)
    {
        Assert.False(EngineWindow.IsLiveInteractionEvent(eventId));
    }

    [Fact]
    public void MainAndLiveInteractionFramesShareOneContinuousClock()
    {
        var timestamps = new Queue<long>([100, 110, 125, 140]);
        var dispatcher = new WindowFrameDispatcher(7, timestamps.Dequeue, 10);
        var frameTimes = new List<double>();

        Assert.True(dispatcher.TryDispatch(7, frameTimes.Add));
        Assert.True(dispatcher.TryDispatch(7, frameTimes.Add));
        Assert.True(dispatcher.TryDispatch(7, frameTimes.Add));

        Assert.Equal([1.0, 1.5, 1.5], frameTimes);
    }

    [Fact]
    public void CallbackOnAnotherThread_CannotRunAFrameOrAdvanceTheClock()
    {
        var timestamps = new Queue<long>([100, 130]);
        var dispatcher = new WindowFrameDispatcher(7, timestamps.Dequeue, 10);
        var frameTimes = new List<double>();

        Assert.False(dispatcher.TryDispatch(8, frameTimes.Add));
        Assert.True(dispatcher.TryDispatch(7, frameTimes.Add));

        Assert.Equal([3.0], frameTimes);
    }

    [Fact]
    public void ReentrantDispatch_IsIgnored()
    {
        var timestamps = new Queue<long>([100, 110]);
        var dispatcher = new WindowFrameDispatcher(7, timestamps.Dequeue, 10);
        var nestedDispatched = true;

        Assert.True(dispatcher.TryDispatch(7, _ =>
            nestedDispatched = dispatcher.TryDispatch(7, _ => { })));

        Assert.False(nestedDispatched);
    }

    [Fact]
    public void FailedFrame_ReleasesTheDispatchGuard()
    {
        var timestamps = new Queue<long>([100, 110, 120]);
        var dispatcher = new WindowFrameDispatcher(7, timestamps.Dequeue, 10);

        Assert.Throws<InvalidOperationException>(() =>
            dispatcher.TryDispatch(7, _ => throw new InvalidOperationException("injected frame failure")));

        Assert.True(dispatcher.TryDispatch(7, _ => { }));
    }
}
