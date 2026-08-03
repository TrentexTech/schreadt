using Silk.NET.Maths;

namespace Schreadt_Engine.Animation.Tweening;

public enum TweenLoopMode
{
    Restart,
    Yoyo
}

public abstract class Tween
{
    public const int RepeatForever = -1;

    public bool IsStarted { get; private set; }

    public bool IsComplete { get; private set; }

    public abstract double TotalDuration { get; }

    public event Action<Tween>? Started;

    public event Action<Tween>? CycleCompleted;

    public event Action<Tween>? Completed;

    public void Restart()
    {
        IsStarted = false;
        IsComplete = false;
        RestartCore();
    }

    internal abstract double Advance(double dt);

    protected abstract void RestartCore();

    protected void MarkStarted()
    {
        if (IsStarted) return;
        IsStarted = true;
        Started?.Invoke(this);
    }

    protected void MarkCycleCompleted() => CycleCompleted?.Invoke(this);

    protected void MarkCompleted()
    {
        if (IsComplete) return;
        IsComplete = true;
        Completed?.Invoke(this);
    }

    protected static void ValidateDeltaTime(double dt)
    {
        if (!double.IsFinite(dt) || dt < 0.0)
            throw new ArgumentOutOfRangeException(nameof(dt), "Delta time must be finite and non-negative.");
    }
}

public sealed class PropertyTween<T> : Tween
{
    private readonly Func<T> _getCurrentValue;
    private readonly Action<T> _setValue;
    private readonly Func<T, T, double, T> _interpolate;
    private T _startValue = default!;
    private bool _valueCaptured;
    private double _delay;
    private double _elapsedDelay;
    private double _elapsedCycle;
    private int _repeatCount;
    private int _completedCycles;
    private TweenLoopMode _loopMode;
    private Func<double, double> _easing = TweenEasings.Linear;

    public PropertyTween(
        Func<T> getCurrentValue,
        Action<T> setValue,
        T endValue,
        double duration,
        Func<T, T, double, T> interpolate)
    {
        ArgumentNullException.ThrowIfNull(getCurrentValue);
        ArgumentNullException.ThrowIfNull(setValue);
        ArgumentNullException.ThrowIfNull(interpolate);
        if (!double.IsFinite(duration) || duration <= 0.0)
            throw new ArgumentOutOfRangeException(nameof(duration), "Tween duration must be finite and greater than zero.");

        _getCurrentValue = getCurrentValue;
        _setValue = setValue;
        _interpolate = interpolate;
        EndValue = endValue;
        Duration = duration;
    }

    public T EndValue { get; }

    public double Duration { get; }

    public double Delay
    {
        get => _delay;
        set
        {
            if (!double.IsFinite(value) || value < 0.0)
                throw new ArgumentOutOfRangeException(nameof(value), "Tween delay must be finite and non-negative.");
            _delay = value;
        }
    }

    public int RepeatCount
    {
        get => _repeatCount;
        set
        {
            if (value < RepeatForever)
                throw new ArgumentOutOfRangeException(nameof(value), $"Repeat count must be {RepeatForever} or greater.");
            _repeatCount = value;
        }
    }

    public TweenLoopMode LoopMode
    {
        get => _loopMode;
        set
        {
            if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
            _loopMode = value;
        }
    }

    public Func<double, double> Easing
    {
        get => _easing;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _easing = value;
        }
    }

    public override double TotalDuration => RepeatCount == RepeatForever
        ? double.PositiveInfinity
        : Delay + (Duration * (RepeatCount + 1));

    internal override double Advance(double dt)
    {
        ValidateDeltaTime(dt);
        if (IsComplete) return dt;

        var remaining = dt;
        if (!_valueCaptured)
        {
            var remainingDelay = Delay - _elapsedDelay;
            if (remaining < remainingDelay)
            {
                _elapsedDelay += remaining;
                return 0.0;
            }

            remaining -= remainingDelay;
            _elapsedDelay = Delay;
            _startValue = _getCurrentValue();
            _valueCaptured = true;
            Apply(0.0);
            MarkStarted();
        }

        while (!IsComplete)
        {
            var remainingCycle = Duration - _elapsedCycle;
            var consumed = Math.Min(remaining, remainingCycle);
            _elapsedCycle += consumed;
            remaining -= consumed;
            Apply(_elapsedCycle / Duration);

            if (_elapsedCycle < Duration) return 0.0;

            MarkCycleCompleted();
            if (RepeatCount != RepeatForever && _completedCycles >= RepeatCount)
            {
                MarkCompleted();
                return remaining;
            }

            _completedCycles++;
            _elapsedCycle = 0.0;
            Apply(0.0);
            if (remaining <= 0.0) return 0.0;
        }

        return remaining;
    }

    protected override void RestartCore()
    {
        _valueCaptured = false;
        _elapsedDelay = 0.0;
        _elapsedCycle = 0.0;
        _completedCycles = 0;
    }

    private void Apply(double cycleProgress)
    {
        var reverse = LoopMode == TweenLoopMode.Yoyo && (_completedCycles & 1) == 1;
        var directedProgress = reverse ? 1.0 - cycleProgress : cycleProgress;
        var easedProgress = Easing(Math.Clamp(directedProgress, 0.0, 1.0));
        if (!double.IsFinite(easedProgress))
            throw new InvalidOperationException("The tween easing function returned a non-finite value.");

        _setValue(_interpolate(_startValue, EndValue, easedProgress));
    }
}

public sealed class DelayTween : Tween
{
    private double _elapsed;

    public DelayTween(double duration)
    {
        if (!double.IsFinite(duration) || duration < 0.0)
            throw new ArgumentOutOfRangeException(nameof(duration), "Delay duration must be finite and non-negative.");
        Duration = duration;
    }

    public double Duration { get; }

    public override double TotalDuration => Duration;

    internal override double Advance(double dt)
    {
        ValidateDeltaTime(dt);
        if (IsComplete) return dt;

        MarkStarted();
        var consumed = Math.Min(dt, Duration - _elapsed);
        _elapsed += consumed;
        if (_elapsed >= Duration) MarkCompleted();
        return IsComplete ? dt - consumed : 0.0;
    }

    protected override void RestartCore() => _elapsed = 0.0;
}

public sealed class CallbackTween : Tween
{
    private readonly Action _callback;

    public CallbackTween(Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        _callback = callback;
    }

    public override double TotalDuration => 0.0;

    internal override double Advance(double dt)
    {
        ValidateDeltaTime(dt);
        if (IsComplete) return dt;

        MarkStarted();
        _callback();
        MarkCycleCompleted();
        MarkCompleted();
        return dt;
    }

    protected override void RestartCore()
    {
    }
}

public sealed class TweenSequence : Tween
{
    private readonly Tween[] _children;
    private int _currentIndex;

    public TweenSequence(IEnumerable<Tween> children)
    {
        ArgumentNullException.ThrowIfNull(children);
        _children = children.ToArray();
        if (_children.Any(child => child is null))
            throw new ArgumentException("A tween sequence cannot contain null entries.", nameof(children));
    }

    public IReadOnlyList<Tween> Children => Array.AsReadOnly(_children);

    public override double TotalDuration => _children.Sum(child => child.TotalDuration);

    internal override double Advance(double dt)
    {
        ValidateDeltaTime(dt);
        if (IsComplete) return dt;

        MarkStarted();
        var remaining = dt;
        while (_currentIndex < _children.Length)
        {
            remaining = _children[_currentIndex].Advance(remaining);
            if (!_children[_currentIndex].IsComplete) return 0.0;
            _currentIndex++;
        }

        MarkCycleCompleted();
        MarkCompleted();
        return remaining;
    }

    protected override void RestartCore()
    {
        _currentIndex = 0;
        foreach (var child in _children) child.Restart();
    }
}

public sealed class TweenParallel : Tween
{
    private readonly Tween[] _children;

    public TweenParallel(IEnumerable<Tween> children)
    {
        ArgumentNullException.ThrowIfNull(children);
        _children = children.ToArray();
        if (_children.Any(child => child is null))
            throw new ArgumentException("A parallel tween cannot contain null entries.", nameof(children));
    }

    public IReadOnlyList<Tween> Children => Array.AsReadOnly(_children);

    public override double TotalDuration => _children.Length == 0 ? 0.0 : _children.Max(child => child.TotalDuration);

    internal override double Advance(double dt)
    {
        ValidateDeltaTime(dt);
        if (IsComplete) return dt;

        MarkStarted();
        var unusedTime = dt;
        foreach (var child in _children)
        {
            unusedTime = Math.Min(unusedTime, child.Advance(dt));
        }

        if (_children.All(child => child.IsComplete))
        {
            MarkCycleCompleted();
            MarkCompleted();
            return unusedTime;
        }

        return 0.0;
    }

    protected override void RestartCore()
    {
        foreach (var child in _children) child.Restart();
    }
}

public static class Tweens
{
    public static PropertyTween<T> To<T>(
        Func<T> getCurrentValue,
        Action<T> setValue,
        T endValue,
        double duration,
        Func<T, T, double, T> interpolate)
    {
        return new PropertyTween<T>(getCurrentValue, setValue, endValue, duration, interpolate);
    }

    public static PropertyTween<double> To(
        Func<double> getCurrentValue,
        Action<double> setValue,
        double endValue,
        double duration)
    {
        return To(getCurrentValue, setValue, endValue, duration,
            static (start, end, progress) => start + ((end - start) * progress));
    }

    public static PropertyTween<float> To(
        Func<float> getCurrentValue,
        Action<float> setValue,
        float endValue,
        double duration)
    {
        return To(getCurrentValue, setValue, endValue, duration,
            static (start, end, progress) => start + ((end - start) * (float)progress));
    }

    public static PropertyTween<Vector2D<double>> To(
        Func<Vector2D<double>> getCurrentValue,
        Action<Vector2D<double>> setValue,
        Vector2D<double> endValue,
        double duration)
    {
        return To(getCurrentValue, setValue, endValue, duration,
            static (start, end, progress) => new Vector2D<double>(
                start.X + ((end.X - start.X) * progress),
                start.Y + ((end.Y - start.Y) * progress)));
    }

    public static PropertyTween<Vector4D<float>> To(
        Func<Vector4D<float>> getCurrentValue,
        Action<Vector4D<float>> setValue,
        Vector4D<float> endValue,
        double duration)
    {
        return To(getCurrentValue, setValue, endValue, duration,
            static (start, end, progress) => new Vector4D<float>(
                start.X + ((end.X - start.X) * (float)progress),
                start.Y + ((end.Y - start.Y) * (float)progress),
                start.Z + ((end.Z - start.Z) * (float)progress),
                start.W + ((end.W - start.W) * (float)progress)));
    }

    public static DelayTween Delay(double duration) => new(duration);

    public static CallbackTween Callback(Action callback) => new(callback);

    public static TweenSequence Sequence(params Tween[] children) => new(children);

    public static TweenParallel Parallel(params Tween[] children) => new(children);
}
