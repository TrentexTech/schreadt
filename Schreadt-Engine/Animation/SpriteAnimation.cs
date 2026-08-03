using System.Collections.ObjectModel;
using Schreadt_Engine.Component;
using Schreadt_Engine.Component.Logic;
using Schreadt_Engine.Core;

namespace Schreadt_Engine.Animation;

public enum SpriteAnimationLoopMode
{
    Once,
    Loop,
    PingPong
}

public readonly record struct SpriteAnimationFrame
{
    public TextureRegion Region { get; }
    public double Duration { get; }

    public SpriteAnimationFrame(TextureRegion region, double duration)
    {
        region.Validate();
        if (!double.IsFinite(duration) || duration <= 0.0)
            throw new ArgumentOutOfRangeException(nameof(duration), "Frame duration must be finite and greater than zero.");

        Region = region;
        Duration = duration;
    }
}

public sealed class SpriteAnimationClip
{
    private readonly SpriteAnimationFrame[] _frames;

    public IReadOnlyList<SpriteAnimationFrame> Frames { get; }

    public SpriteAnimationLoopMode LoopMode { get; }

    public double Duration => _frames.Sum(frame => frame.Duration);

    public SpriteAnimationClip(
        IEnumerable<SpriteAnimationFrame> frames,
        SpriteAnimationLoopMode loopMode = SpriteAnimationLoopMode.Loop)
    {
        ArgumentNullException.ThrowIfNull(frames);
        if (!Enum.IsDefined(loopMode)) throw new ArgumentOutOfRangeException(nameof(loopMode));

        _frames = frames.ToArray();
        if (_frames.Length == 0) throw new ArgumentException("An animation clip requires at least one frame.", nameof(frames));

        // Reconstructing validates values supplied by serializers that can bypass constructors.
        for (var index = 0; index < _frames.Length; index++)
            _frames[index] = new SpriteAnimationFrame(_frames[index].Region, _frames[index].Duration);

        Frames = Array.AsReadOnly(_frames);
        LoopMode = loopMode;
    }

    public static SpriteAnimationClip FromGrid(
        int columns,
        int rows,
        IEnumerable<int> frameIndices,
        double frameDuration,
        SpriteAnimationLoopMode loopMode = SpriteAnimationLoopMode.Loop)
    {
        ArgumentNullException.ThrowIfNull(frameIndices);
        if (columns <= 0) throw new ArgumentOutOfRangeException(nameof(columns));
        if (rows <= 0) throw new ArgumentOutOfRangeException(nameof(rows));

        var cellCount = checked(columns * rows);
        var frames = frameIndices.Select(index =>
        {
            if (index < 0 || index >= cellCount) throw new ArgumentOutOfRangeException(nameof(frameIndices));
            return new SpriteAnimationFrame(
                TextureRegion.FromGridCell(index % columns, index / columns, columns, rows),
                frameDuration);
        });
        return new SpriteAnimationClip(frames, loopMode);
    }
}

public interface ISpriteRegionTarget
{
    TextureRegion Region { get; set; }
}

public readonly record struct SpriteAnimationFrameChanged(
    string AnimationName,
    int FrameIndex,
    SpriteAnimationFrame Frame);

public sealed class SpriteAnimator : GameComponent, IInitializable, IUpdateable, IShutdownable
{
    private readonly Dictionary<string, SpriteAnimationClip> _clips = new(StringComparer.Ordinal);
    private readonly IReadOnlyDictionary<string, SpriteAnimationClip> _readOnlyClips;
    private ISpriteRegionTarget? _target;
    private TextureRegion _initialRegion;
    private SpriteAnimationClip? _currentClip;
    private string? _currentAnimationName;
    private string? _autoPlayAnimation;
    private int _frameIndex;
    private int _direction = 1;
    private double _elapsedFrameTime;
    private double _speed = 1.0;
    private int _playbackVersion;

    public SpriteAnimator()
    {
        _readOnlyClips = new ReadOnlyDictionary<string, SpriteAnimationClip>(_clips);
    }

    public IReadOnlyDictionary<string, SpriteAnimationClip> Clips => _readOnlyClips;

    public string? CurrentAnimationName => _currentAnimationName;

    public SpriteAnimationClip? CurrentClip => _currentClip;

    public int CurrentFrameIndex => _frameIndex;

    public double ElapsedFrameTime => _elapsedFrameTime;

    public bool IsPlaying { get; private set; }

    public string? AutoPlayAnimation
    {
        get => _autoPlayAnimation;
        set
        {
            if (value is not null) ArgumentException.ThrowIfNullOrWhiteSpace(value);
            _autoPlayAnimation = value?.Trim();
        }
    }

    public double Speed
    {
        get => _speed;
        set
        {
            if (!double.IsFinite(value) || value <= 0.0)
                throw new ArgumentOutOfRangeException(nameof(value), "Animation speed must be finite and greater than zero.");
            _speed = value;
        }
    }

    public event Action<SpriteAnimationFrameChanged>? FrameChanged;

    public event Action<string>? Looped;

    public event Action<string>? Completed;

    public void AddClip(string name, SpriteAnimationClip clip, bool replaceExisting = false)
    {
        var normalizedName = NormalizeName(name);
        ArgumentNullException.ThrowIfNull(clip);

        if (_clips.ContainsKey(normalizedName) && !replaceExisting)
            throw new InvalidOperationException($"An animation named '{normalizedName}' is already registered.");

        _clips[normalizedName] = clip;
        if (replaceExisting && string.Equals(_currentAnimationName, normalizedName, StringComparison.Ordinal))
            Play(normalizedName);
    }

    public bool RemoveClip(string name)
    {
        var normalizedName = NormalizeName(name);
        if (!_clips.Remove(normalizedName)) return false;

        if (string.Equals(_currentAnimationName, normalizedName, StringComparison.Ordinal))
        {
            IsPlaying = false;
            _currentClip = null;
            _currentAnimationName = null;
            _frameIndex = 0;
            _elapsedFrameTime = 0.0;
            _playbackVersion++;
        }

        return true;
    }

    public void Play(string name, bool restart = true)
    {
        var normalizedName = NormalizeName(name);
        if (!_clips.TryGetValue(normalizedName, out var clip))
            throw new KeyNotFoundException($"No animation named '{normalizedName}' is registered.");

        if (!restart && ReferenceEquals(_currentClip, clip))
        {
            IsPlaying = true;
            return;
        }

        _currentAnimationName = normalizedName;
        _currentClip = clip;
        _frameIndex = 0;
        _direction = 1;
        _elapsedFrameTime = 0.0;
        IsPlaying = true;
        _playbackVersion++;
        ApplyCurrentFrame(notify: true);
    }

    public void Pause()
    {
        if (!IsPlaying) return;
        IsPlaying = false;
        _playbackVersion++;
    }

    public void Resume()
    {
        if (_currentClip is null || IsPlaying) return;
        IsPlaying = true;
        _playbackVersion++;
    }

    public void Stop(bool resetToFirstFrame = true)
    {
        IsPlaying = false;
        _elapsedFrameTime = 0.0;
        _direction = 1;
        _playbackVersion++;

        if (resetToFirstFrame && _currentClip is not null)
        {
            _frameIndex = 0;
            ApplyCurrentFrame(notify: true);
        }
    }

    public void Init()
    {
        if (_currentClip is null && AutoPlayAnimation is not null) Play(AutoPlayAnimation);
        else ApplyCurrentFrame(notify: false);
    }

    public void Update(double dt)
    {
        if (!double.IsFinite(dt) || dt < 0.0)
            throw new ArgumentOutOfRangeException(nameof(dt), "Delta time must be finite and non-negative.");
        if (!IsPlaying || _currentClip is null || dt == 0.0) return;

        var scaledDeltaTime = dt * Speed;
        if (!double.IsFinite(scaledDeltaTime))
            throw new InvalidOperationException("The scaled animation delta time is too large.");

        _elapsedFrameTime += scaledDeltaTime;
        var playbackVersion = _playbackVersion;

        while (IsPlaying && _currentClip is not null && playbackVersion == _playbackVersion)
        {
            var frameDuration = _currentClip.Frames[_frameIndex].Duration;
            if (_elapsedFrameTime < frameDuration) break;

            _elapsedFrameTime -= frameDuration;
            AdvanceFrame();
        }
    }

    public void Shutdown()
    {
        IsPlaying = false;
        _elapsedFrameTime = 0.0;
        _direction = 1;
        if (AutoPlayAnimation is not null)
        {
            _currentClip = null;
            _currentAnimationName = null;
            _frameIndex = 0;
        }
        _playbackVersion++;
    }

    protected override void OnAttached()
    {
        _target = Owner as ISpriteRegionTarget
            ?? throw new InvalidOperationException($"{nameof(SpriteAnimator)} requires an owner implementing {nameof(ISpriteRegionTarget)}.");
        _initialRegion = _target.Region;
        ApplyCurrentFrame(notify: false);
    }

    protected override void OnDetached()
    {
        if (_target is not null) _target.Region = _initialRegion;
        _target = null;
        IsPlaying = false;
        _playbackVersion++;
    }

    private void AdvanceFrame()
    {
        var clip = _currentClip!;
        var name = _currentAnimationName!;
        var playbackVersion = _playbackVersion;
        var looped = false;

        switch (clip.LoopMode)
        {
            case SpriteAnimationLoopMode.Once:
                if (_frameIndex == clip.Frames.Count - 1)
                {
                    IsPlaying = false;
                    _elapsedFrameTime = 0.0;
                    Completed?.Invoke(name);
                    return;
                }

                _frameIndex++;
                break;

            case SpriteAnimationLoopMode.Loop:
                _frameIndex++;
                if (_frameIndex >= clip.Frames.Count)
                {
                    _frameIndex = 0;
                    looped = true;
                }
                break;

            case SpriteAnimationLoopMode.PingPong:
                looped = AdvancePingPong(clip);
                break;

            default:
                throw new InvalidOperationException($"Unknown animation loop mode '{clip.LoopMode}'.");
        }

        ApplyCurrentFrame(notify: true);
        if (looped && playbackVersion == _playbackVersion) Looped?.Invoke(name);
    }

    private bool AdvancePingPong(SpriteAnimationClip clip)
    {
        if (clip.Frames.Count == 1)
        {
            return true;
        }

        var nextFrame = _frameIndex + _direction;
        if (nextFrame >= clip.Frames.Count)
        {
            _direction = -1;
            nextFrame = clip.Frames.Count - 2;
        }

        var looped = _direction < 0 && nextFrame == 0;
        if (looped)
        {
            _direction = 1;
        }

        _frameIndex = nextFrame;
        return looped;
    }

    private void ApplyCurrentFrame(bool notify)
    {
        if (_target is null || _currentClip is null || _currentAnimationName is null) return;

        var frame = _currentClip.Frames[_frameIndex];
        _target.Region = frame.Region;
        if (notify) FrameChanged?.Invoke(new SpriteAnimationFrameChanged(_currentAnimationName, _frameIndex, frame));
    }

    private static string NormalizeName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return name.Trim();
    }
}
