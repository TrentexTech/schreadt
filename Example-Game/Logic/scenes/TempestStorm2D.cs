using Schreadt_Engine.Component;
using Schreadt_Engine.Core;
using Silk.NET.Maths;

namespace Example_Game.Logic.scenes;

internal sealed class TempestStorm2D
{
    internal const double CloudSpacing = 3.4;
    internal const double CloudSpeed = 0.18;
    internal const double RainTravel = 2.3;
    internal const double RainSpeed = 0.52;

    private readonly Random _random;
    private readonly double[] _boltJitter = new double[7];
    private int _cloudPatternCycle;
    private double _secondsUntilLightning = 1.25;
    private double _lightningRemaining;
    private double _boltX = 0.45;

    internal TempestStorm2D(int randomSeed = 5173)
    {
        _random = new Random(randomSeed);
        Clouds = new TempestCloudBackgroundLayer(this);
        Lightning = new LightningCompositionPass(this);
        Rain = new RainCompositionPass(this);
        ScreenFlash = new ScreenFlashCompositionPass(this);
    }

    internal IBackground2D Clouds { get; }
    internal IFrameCompositionPass2D Lightning { get; }
    internal IFrameCompositionPass2D Rain { get; }
    internal IFrameCompositionPass2D ScreenFlash { get; }
    internal double CloudOffset { get; private set; }
    internal double RainPhase { get; private set; }
    internal double LightningIntensity { get; private set; }

    internal void Update(double dt)
    {
        if (!double.IsFinite(dt) || dt < 0.0)
            throw new ArgumentOutOfRangeException(nameof(dt), "Storm delta time must be finite and non-negative.");

        var advancedCloudOffset = CloudOffset + dt * CloudSpeed;
        var completedCloudCycles = Math.Floor(advancedCloudOffset / CloudSpacing);
        CloudOffset = advancedCloudOffset - completedCloudCycles * CloudSpacing;
        _cloudPatternCycle = PositiveModulo(
            _cloudPatternCycle + (int)(completedCloudCycles % 3.0),
            3);
        RainPhase = (RainPhase + dt * RainSpeed) % RainTravel;

        if (_lightningRemaining > 0.0)
        {
            _lightningRemaining = Math.Max(0.0, _lightningRemaining - dt);
            var fade = Math.Clamp(_lightningRemaining / 0.5, 0.0, 1.0);
            var flicker = 0.72 + Math.Abs(Math.Sin(_lightningRemaining * 73.0)) * 0.28;
            LightningIntensity = fade * flicker;
            return;
        }

        LightningIntensity = 0.0;
        _secondsUntilLightning -= dt;
        if (_secondsUntilLightning <= 0.0) TriggerLightning();
    }

    internal void TriggerLightning()
    {
        _lightningRemaining = 0.52;
        LightningIntensity = 1.0;
        _secondsUntilLightning = 3.8 + _random.NextDouble() * 3.2;
        _boltX = -0.65 + _random.NextDouble() * 1.3;
        for (var index = 0; index < _boltJitter.Length; index++)
            _boltJitter[index] = (_random.NextDouble() - 0.5) * 0.18;
    }

    private static int PositiveModulo(long value, int modulus)
    {
        var remainder = (int)(value % modulus);
        return remainder < 0 ? remainder + modulus : remainder;
    }

    private sealed class TempestCloudBackgroundLayer(TempestStorm2D storm) : IBackground2D
    {
        private static readonly Vector4D<float> ShadowColor = new(0.055f, 0.09f, 0.16f, 0.72f);
        private static readonly Vector4D<float> EdgeColor = new(0.13f, 0.22f, 0.32f, 0.62f);

        public bool Enabled { get; set; } = true;
        public double ParallaxFactor => 0.22;
        public Vector2D<double> ParallaxOrigin => Vector2D<double>.Zero;

        public void Render(IBackgroundRenderContext2D context)
        {
            var view = context.View;
            var firstCloudIndex = (long)Math.Floor(
                (view.VisibleMinimum.X - storm.CloudOffset) / CloudSpacing);
            for (var cloudIndex = firstCloudIndex; ; cloudIndex++)
            {
                var x = cloudIndex * CloudSpacing + storm.CloudOffset;
                if (x > view.VisibleMaximum.X + CloudSpacing) break;

                var stablePatternIndex = PositiveModulo(cloudIndex - storm._cloudPatternCycle, 3);
                var y = view.VisibleMaximum.Y - 0.55 - stablePatternIndex * 0.24;
                DrawCloud(context, new Vector2D<double>(x, y));
            }
        }

        private static void DrawCloud(IRenderContext2D context, Vector2D<double> center)
        {
            context.DrawCircle(center + new Vector2D<double>(-0.45, 0.0), 0.44, ShadowColor);
            context.DrawCircle(center + new Vector2D<double>(0.0, 0.14), 0.58, ShadowColor);
            context.DrawCircle(center + new Vector2D<double>(0.52, -0.02), 0.4, EdgeColor);
            context.DrawRectangle(
                center + new Vector2D<double>(0.03, -0.22),
                new Vector2D<double>(1.45, 0.42),
                ShadowColor);
        }
    }

    private sealed class LightningCompositionPass(TempestStorm2D storm) : IFrameCompositionPass2D
    {
        public string Name => "Lightning";
        public FrameCompositionStage Stage => FrameCompositionStage.BeforeScene;
        public int Order => 0;
        public bool Enabled => storm.LightningIntensity > 0.01;

        public void Render(IFrameCompositionContext2D context)
        {
            var lines = new List<LineSegment2D>(storm._boltJitter.Length);
            var previous = context.View.NormalizedDeviceToWorldPoint(new Vector2D<double>(storm._boltX, 1.05));
            for (var index = 0; index < storm._boltJitter.Length; index++)
            {
                var progress = (index + 1.0) / storm._boltJitter.Length;
                var next = context.View.NormalizedDeviceToWorldPoint(new Vector2D<double>(
                    storm._boltX + storm._boltJitter[index],
                    1.05 - progress * 1.4));
                lines.Add(new LineSegment2D(previous, next));
                previous = next;
            }

            var intensity = (float)storm.LightningIntensity;
            context.DrawLines(lines, new Vector4D<float>(0.68f, 0.9f, 1.0f, 0.45f + intensity * 0.55f));
        }
    }

    private sealed class RainCompositionPass(TempestStorm2D storm) : IFrameCompositionPass2D
    {
        private const int DropCount = 72;

        public string Name => "Rain";
        public FrameCompositionStage Stage => FrameCompositionStage.AfterScene;
        public int Order => 0;
        public bool Enabled => true;

        public void Render(IFrameCompositionContext2D context)
        {
            var lines = new List<LineSegment2D>(DropCount);
            for (var index = 0; index < DropCount; index++)
            {
                var x = -1.08 + Hash(index * 31 + 7) * 2.16;
                var y = RainTravel * 0.5 -
                        (Hash(index * 47 + 13) * RainTravel + storm.RainPhase) % RainTravel;
                var start = context.View.NormalizedDeviceToWorldPoint(new Vector2D<double>(x, y));
                var end = context.View.NormalizedDeviceToWorldPoint(new Vector2D<double>(x - 0.025, y - 0.13));
                lines.Add(new LineSegment2D(start, end));
            }

            context.DrawLines(lines, new Vector4D<float>(0.46f, 0.78f, 0.94f, 0.48f));
        }

        private static double Hash(int value)
        {
            var x = Math.Sin(value * 12.9898) * 43758.5453;
            return x - Math.Floor(x);
        }
    }

    private sealed class ScreenFlashCompositionPass(TempestStorm2D storm) : IFrameCompositionPass2D
    {
        public string Name => "Screen Flash";
        public FrameCompositionStage Stage => FrameCompositionStage.BeforeGui;
        public int Order => 0;
        public bool Enabled => storm.LightningIntensity > 0.01;

        public void Render(IFrameCompositionContext2D context)
        {
            var alpha = (float)(storm.LightningIntensity * 0.22);
            context.DrawScreenRectangle(
                Vector2D<float>.Zero,
                new Vector2D<float>(context.ViewportSize.X, context.ViewportSize.Y),
                new Vector4D<float>(0.68f, 0.86f, 1.0f, alpha));
        }
    }
}
