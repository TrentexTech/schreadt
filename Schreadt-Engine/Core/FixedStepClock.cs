namespace Schreadt_Engine.Core;

internal sealed class FixedStepClock
{
    internal const double FixedDeltaTime = 1.0 / 60.0;
    internal const double MaximumFrameDeltaTime = 0.25;
    internal const int MaximumStepsPerFrame = 8;

    private const double StepComparisonTolerance = 1e-12;
    private double _accumulator;

    internal FrameTiming Advance(double frameDeltaTime)
    {
        if (!double.IsFinite(frameDeltaTime) || frameDeltaTime < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(frameDeltaTime),
                "Frame delta time must be finite and non-negative.");
        }

        var clampedFrameDeltaTime = Math.Min(frameDeltaTime, MaximumFrameDeltaTime);
        _accumulator += clampedFrameDeltaTime;
        var fixedStepCount = 0;

        while (_accumulator + StepComparisonTolerance >= FixedDeltaTime
               && fixedStepCount < MaximumStepsPerFrame)
        {
            _accumulator = Math.Max(0.0, _accumulator - FixedDeltaTime);
            fixedStepCount++;
        }

        if (_accumulator >= FixedDeltaTime)
        {
            _accumulator %= FixedDeltaTime;

            if (_accumulator + StepComparisonTolerance >= FixedDeltaTime)
            {
                _accumulator = 0.0;
            }
        }

        return new FrameTiming(clampedFrameDeltaTime, fixedStepCount);
    }
}

internal readonly record struct FrameTiming(double FrameDeltaTime, int FixedStepCount);
