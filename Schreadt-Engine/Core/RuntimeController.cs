namespace Schreadt_Engine.Core;

/// <summary>
/// Controls simulation flow independently from rendering, input, and GUI updates.
/// </summary>
public sealed class RuntimeController
{
    private readonly FixedStepClock _physicsClock = new();
    private bool _isPaused;
    private bool _manualPause;
    private int _pauseRequestCount;
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

            if (_timeScale == value) return;
            var previous = _timeScale;
            _timeScale = value;
            EngineLog.Information($"Time scale changed from {previous:G4} to {value:G4}.", "Runtime");
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
        if (_manualPause) return;
        _manualPause = true;
        RefreshPauseState();
    }

    public void Resume()
    {
        if (!_manualPause) return;
        _manualPause = false;
        RefreshPauseState();
    }

    public void TogglePause()
    {
        if (_manualPause) Resume();
        else Pause();
    }

    /// <summary>Queues one simulation update and one fixed tick while paused.</summary>
    public void StepOneFrame()
    {
        if (!_isPaused)
            throw new InvalidOperationException("Single-step is only available while the runtime is paused.");

        _singleStepPending = true;
        EngineLog.Debug("Queued one simulation frame while paused.", "Runtime");
    }

    internal void AcquirePauseRequest()
    {
        _pauseRequestCount = checked(_pauseRequestCount + 1);
        EngineLog.Trace($"Pause request acquired; active requests: {_pauseRequestCount}.", "Runtime");
        RefreshPauseState();
    }

    internal void ReleasePauseRequest()
    {
        if (_pauseRequestCount <= 0)
            throw new InvalidOperationException("There is no runtime pause request to release.");
        _pauseRequestCount--;
        EngineLog.Trace($"Pause request released; active requests: {_pauseRequestCount}.", "Runtime");
        RefreshPauseState();
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

    private void RefreshPauseState()
    {
        var paused = _manualPause || _pauseRequestCount > 0;
        if (_isPaused == paused) return;

        _isPaused = paused;
        _singleStepPending = false;
        _physicsClock.Reset();
        EngineLog.Information(
            paused
                ? $"Simulation paused (manual: {_manualPause}; pause requests: {_pauseRequestCount})."
                : "Simulation resumed.",
            "Runtime");
        PauseStateChanged?.Invoke(paused);
    }
}

internal readonly record struct RuntimeFrameTiming(
    double FrameDeltaTime,
    int FixedStepCount,
    bool ShouldUpdateSimulation);
