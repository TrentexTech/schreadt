using System.Diagnostics;

namespace Schreadt_Engine.Core;

internal sealed class WindowFrameDispatcher
{
    private readonly int _ownerThreadId;
    private readonly Func<long> _getTimestamp;
    private readonly double _timestampFrequency;
    private long _previousTimestamp;
    private int _dispatching;

    internal WindowFrameDispatcher(int ownerThreadId)
        : this(ownerThreadId, Stopwatch.GetTimestamp, Stopwatch.Frequency)
    {
    }

    internal WindowFrameDispatcher(int ownerThreadId, Func<long> getTimestamp, long timestampFrequency)
    {
        if (timestampFrequency <= 0) throw new ArgumentOutOfRangeException(nameof(timestampFrequency));
        ArgumentNullException.ThrowIfNull(getTimestamp);

        _ownerThreadId = ownerThreadId;
        _getTimestamp = getTimestamp;
        _timestampFrequency = timestampFrequency;
        _previousTimestamp = getTimestamp();
    }

    internal bool IsOwnerThread(int threadId) => threadId == _ownerThreadId;

    internal bool TryDispatch(int threadId, Action<double> frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        if (!IsOwnerThread(threadId)) return false;
        if (Interlocked.Exchange(ref _dispatching, 1) != 0) return false;

        try
        {
            var timestamp = _getTimestamp();
            var frameTime = Math.Max(0L, timestamp - _previousTimestamp) / _timestampFrequency;
            _previousTimestamp = timestamp;
            frame(frameTime);
            return true;
        }
        finally
        {
            Volatile.Write(ref _dispatching, 0);
        }
    }
}
