using Schreadt_Engine.Component;
using Schreadt_Engine.Core;
using Silk.NET.Maths;

namespace Example_Game.Logic.scenes;

internal sealed class KineticFoundryBackground : IBackground2D
{
    public bool Enabled { get; set; } = true;
    public double ParallaxFactor => 0.28;
    public Vector2D<double> ParallaxOrigin => Vector2D<double>.Zero;

    public void Render(IBackgroundRenderContext2D renderer)
    {
        var view = renderer.View;
        var first = (int)Math.Floor(view.VisibleMinimum.X / 3.2) - 1;
        var last = (int)Math.Ceiling(view.VisibleMaximum.X / 3.2) + 1;
        for (var index = first; index <= last; index++)
        {
            var x = index * 3.2;
            var height = 1.25 + PositiveModulo(index, 3) * 0.42;
            renderer.DrawRectangle(
                new Vector2D<double>(x, -1.15 + height * 0.5),
                new Vector2D<double>(2.35, height),
                new Vector4D<float>(0.09f, 0.12f, 0.16f, 0.9f));
            renderer.DrawRectangle(
                new Vector2D<double>(x - 0.58, -0.25),
                new Vector2D<double>(0.22, 2.65),
                new Vector4D<float>(0.17f, 0.13f, 0.13f, 0.92f));
            renderer.DrawCircle(
                new Vector2D<double>(x + 0.45, -0.28),
                0.17,
                new Vector4D<float>(0.96f, 0.36f, 0.08f, 0.56f));
        }
    }

    private static int PositiveModulo(int value, int modulus)
    {
        var result = value % modulus;
        return result < 0 ? result + modulus : result;
    }
}

internal sealed class KineticFoundryEffects
{
    internal const double CycleDuration = 6.0;
    internal const double SparkTravel = 1.25;
    private double _cycleElapsed;
    private double _ignitionRemaining;

    internal KineticFoundryEffects()
    {
        Heat = new HeatCompositionPass(this);
        Sparks = new SparkCompositionPass(this);
        Flash = new FlashCompositionPass(this);
    }

    internal IFrameCompositionPass2D Heat { get; }
    internal IFrameCompositionPass2D Sparks { get; }
    internal IFrameCompositionPass2D Flash { get; }
    internal bool IgnitionActive => _ignitionRemaining > 0.0;
    internal double SparkOffset => _cycleElapsed * SparkTravel / CycleDuration;
    internal double HeatPulse => Math.Sin(_cycleElapsed * Math.Tau / CycleDuration);

    internal void Update(double dt)
    {
        if (!double.IsFinite(dt) || dt < 0.0)
            throw new ArgumentOutOfRangeException(nameof(dt), "Effect delta time must be finite and non-negative.");

        _cycleElapsed = (_cycleElapsed + dt) % CycleDuration;
        _ignitionRemaining = Math.Max(0.0, _ignitionRemaining - dt);
    }

    internal void TriggerIgnition() => _ignitionRemaining = 0.72;

    private sealed class HeatCompositionPass(KineticFoundryEffects effects) : IFrameCompositionPass2D
    {
        public string Name => "Foundry Heat";
        public FrameCompositionStage Stage => FrameCompositionStage.BeforeScene;
        public int Order => 0;
        public bool Enabled => true;

        public void Render(IFrameCompositionContext2D context)
        {
            var pulse = 0.035f + (float)((effects.HeatPulse + 1.0) * 0.012);
            context.DrawScreenRectangle(
                Vector2D<float>.Zero,
                new Vector2D<float>(context.ViewportSize.X, context.ViewportSize.Y),
                new Vector4D<float>(0.34f, 0.08f, 0.02f, pulse));
        }
    }

    private sealed class SparkCompositionPass(KineticFoundryEffects effects) : IFrameCompositionPass2D
    {
        private const int SparkCount = 28;

        public string Name => "Foundry Sparks";
        public FrameCompositionStage Stage => FrameCompositionStage.AfterScene;
        public int Order => 0;
        public bool Enabled => true;

        public void Render(IFrameCompositionContext2D context)
        {
            var lines = new List<LineSegment2D>(SparkCount);
            var intensity = effects.IgnitionActive ? 1.8 : 1.0;
            for (var index = 0; index < SparkCount; index++)
            {
                var x = -1.05 + Hash(index * 37 + 5) * 2.1;
                var y = -1.0 + (Hash(index * 61 + 9) * SparkTravel + effects.SparkOffset) % SparkTravel;
                var start = context.View.NormalizedDeviceToWorldPoint(new Vector2D<double>(x, y));
                var end = context.View.NormalizedDeviceToWorldPoint(
                    new Vector2D<double>(x + 0.018, y + 0.04 * intensity));
                lines.Add(new LineSegment2D(start, end));
            }

            context.DrawLines(lines, new Vector4D<float>(1.0f, 0.54f, 0.08f, 0.72f));
        }

        private static double Hash(int value)
        {
            var hash = Math.Sin(value * 12.9898) * 43758.5453;
            return hash - Math.Floor(hash);
        }
    }

    private sealed class FlashCompositionPass(KineticFoundryEffects effects) : IFrameCompositionPass2D
    {
        public string Name => "Ignition Flash";
        public FrameCompositionStage Stage => FrameCompositionStage.BeforeGui;
        public int Order => 0;
        public bool Enabled => effects.IgnitionActive;

        public void Render(IFrameCompositionContext2D context)
        {
            var alpha = (float)Math.Clamp(effects._ignitionRemaining / 0.72, 0.0, 1.0) * 0.34f;
            context.DrawScreenRectangle(
                Vector2D<float>.Zero,
                new Vector2D<float>(context.ViewportSize.X, context.ViewportSize.Y),
                new Vector4D<float>(1.0f, 0.45f, 0.08f, alpha));
        }
    }
}
