namespace Schreadt_Engine.Core;

/// <summary>
/// Controls simulation flow independently from rendering, input, and GUI updates.
/// </summary>
public sealed class RuntimeController
{
    private readonly FixedStepClock _physicsClock = new();
    private bool _isPaused;
    private bool _singleStepPending;
    private double _timeScale = 1.0;

    public bool IsPaused => _isPaused;

    public bool IsSingleStepPending => _singleStepPending;

    public double TimeScale
    {
        get => _timeScale;
        set
        {
            if (!double.IsFinite(value) || value <= 0.0)
                throw new ArgumentOutOfRangeException(nameof(value), "Time scale must be finite and greater than zero.");

            _timeScale = value;
        }
    }

    /// <summary>The most recent real frame delta, before time scaling.</summary>
    public double UnscaledDeltaTime { get; private set; }

    /// <summary>The most recent simulation delta, after scaling and clamping.</summary>
    public double DeltaTime { get; private set; }

    public double FixedDeltaTime => FixedStepClock.FixedDeltaTime;

    public ulong FrameCount { get; private set; }

    public event Action<bool>? PauseStateChanged;

    public void Pause()
    {
        if (_isPaused) return;

        _isPaused = true;
        _singleStepPending = false;
        _physicsClock.Reset();
        PauseStateChanged?.Invoke(true);
    }

    public void Resume()
    {
        if (!_isPaused) return;

        _isPaused = false;
        _singleStepPending = false;
        _physicsClock.Reset();
        PauseStateChanged?.Invoke(false);
    }

    public void TogglePause()
    {
        if (_isPaused) Resume();
        else Pause();
    }

    /// <summary>Queues one simulation update and one fixed tick while paused.</summary>
    public void StepOneFrame()
    {
        if (!_isPaused)
            throw new InvalidOperationException("Single-step is only available while the runtime is paused.");

        _singleStepPending = true;
    }

    internal RuntimeFrameTiming Advance(double frameDeltaTime)
    {
        if (!double.IsFinite(frameDeltaTime) || frameDeltaTime < 0.0)
            throw new ArgumentOutOfRangeException(
                nameof(frameDeltaTime),
                "Frame delta time must be finite and non-negative.");

        FrameCount++;
        UnscaledDeltaTime = Math.Min(frameDeltaTime, FixedStepClock.MaximumFrameDeltaTime);

        if (_isPaused && !_singleStepPending)
        {
            DeltaTime = 0.0;
            return new RuntimeFrameTiming(0.0, 0, false);
        }

        var isSingleStep = _isPaused && _singleStepPending;
        _singleStepPending = false;

        if (isSingleStep) _physicsClock.Reset();

        var requestedDeltaTime = isSingleStep
            ? FixedStepClock.FixedDeltaTime
            : UnscaledDeltaTime * TimeScale;
        var timing = _physicsClock.Advance(Math.Min(requestedDeltaTime, FixedStepClock.MaximumFrameDeltaTime));
        DeltaTime = timing.FrameDeltaTime;

        return new RuntimeFrameTiming(DeltaTime, timing.FixedStepCount, true);
    }
}

internal readonly record struct RuntimeFrameTiming(
    double FrameDeltaTime,
    int FixedStepCount,
    bool ShouldUpdateSimulation);
