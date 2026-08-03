namespace Schreadt_Engine.Animation.Tweening;

public static class TweenEasings
{
    public static double Linear(double progress) => progress;

    public static double QuadraticIn(double progress) => progress * progress;

    public static double QuadraticOut(double progress) => 1.0 - ((1.0 - progress) * (1.0 - progress));

    public static double QuadraticInOut(double progress)
    {
        return progress < 0.5
            ? 2.0 * progress * progress
            : 1.0 - (Math.Pow(-2.0 * progress + 2.0, 2.0) / 2.0);
    }

    public static double CubicIn(double progress) => progress * progress * progress;

    public static double CubicOut(double progress) => 1.0 - Math.Pow(1.0 - progress, 3.0);

    public static double CubicInOut(double progress)
    {
        return progress < 0.5
            ? 4.0 * progress * progress * progress
            : 1.0 - (Math.Pow(-2.0 * progress + 2.0, 3.0) / 2.0);
    }

    public static double SineIn(double progress) => 1.0 - Math.Cos(progress * Math.PI / 2.0);

    public static double SineOut(double progress) => Math.Sin(progress * Math.PI / 2.0);

    public static double SineInOut(double progress) => -(Math.Cos(Math.PI * progress) - 1.0) / 2.0;

    public static double BackOut(double progress)
    {
        const double overshoot = 1.70158;
        var shifted = progress - 1.0;
        return 1.0 + ((overshoot + 1.0) * shifted * shifted * shifted) + (overshoot * shifted * shifted);
    }
}
