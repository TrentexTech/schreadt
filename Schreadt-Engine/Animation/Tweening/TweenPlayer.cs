using Schreadt_Engine.Component;
using Schreadt_Engine.Component.Logic;

namespace Schreadt_Engine.Animation.Tweening;

public sealed class TweenPlayer : GameComponent, IUpdateable, IShutdownable
{
    private readonly List<Playback> _playbacks = [];
    private double _timeScale = 1.0;

    public int Count => _playbacks.Count;

    public IReadOnlyList<Tween> ActiveTweens => _playbacks.Select(playback => playback.Tween).ToArray();

    public bool Paused { get; set; }

    public double TimeScale
    {
        get => _timeScale;
        set
        {
            if (!double.IsFinite(value) || value < 0.0)
                throw new ArgumentOutOfRangeException(nameof(value), "Tween time scale must be finite and non-negative.");
            _timeScale = value;
        }
    }

    public T Play<T>(T tween) where T : Tween
    {
        ArgumentNullException.ThrowIfNull(tween);
        if (_playbacks.Any(playback => ReferenceEquals(playback.Tween, tween)))
            throw new InvalidOperationException("The tween is already playing on this player.");

        tween.Restart();
        var playback = new Playback(tween);
        _playbacks.Add(playback);
        tween.Advance(0.0);
        if (tween.IsComplete) _playbacks.Remove(playback);
        return tween;
    }

    public bool Contains(Tween tween)
    {
        ArgumentNullException.ThrowIfNull(tween);
        return _playbacks.Any(playback => ReferenceEquals(playback.Tween, tween));
    }

    public bool Pause(Tween tween)
    {
        var playback = Find(tween);
        if (playback is null || playback.Paused) return false;
        playback.Paused = true;
        return true;
    }

    public bool Resume(Tween tween)
    {
        var playback = Find(tween);
        if (playback is null || !playback.Paused) return false;
        playback.Paused = false;
        return true;
    }

    public bool Cancel(Tween tween)
    {
        var playback = Find(tween);
        return playback is not null && _playbacks.Remove(playback);
    }

    public void Clear() => _playbacks.Clear();

    public void Update(double dt)
    {
        if (!double.IsFinite(dt) || dt < 0.0)
            throw new ArgumentOutOfRangeException(nameof(dt), "Delta time must be finite and non-negative.");
        if (Paused || TimeScale == 0.0 || dt == 0.0) return;

        var scaledDeltaTime = dt * TimeScale;
        if (!double.IsFinite(scaledDeltaTime))
            throw new InvalidOperationException("The scaled tween delta time is too large.");

        foreach (var playback in _playbacks.ToArray())
        {
            if (!_playbacks.Contains(playback) || playback.Paused) continue;

            playback.Tween.Advance(scaledDeltaTime);
            if (playback.Tween.IsComplete) _playbacks.Remove(playback);
        }
    }

    public void Shutdown() => Clear();

    protected override void OnDetached() => Clear();

    private Playback? Find(Tween tween)
    {
        ArgumentNullException.ThrowIfNull(tween);
        return _playbacks.FirstOrDefault(playback => ReferenceEquals(playback.Tween, tween));
    }

    private sealed class Playback(Tween tween)
    {
        internal Tween Tween { get; } = tween;
        internal bool Paused { get; set; }
    }
}
