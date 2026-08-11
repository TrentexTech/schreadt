using Schreadt_Engine.Component.Logic;
using Schreadt_Engine.Core;

namespace Mandelbrot_Explorer.Logic;

internal sealed class MandelbrotGameLogic : GameLogic
{
    internal const string ExplorerScene = "mandelbrot-explorer";

    public override void Init()
    {
        var input = Context.Input;
        input.SetActionBindings(MandelbrotInputActions.PanLeft,
            InputBinding.ForKey(InputKey.A), InputBinding.ForKey(InputKey.Left));
        input.SetActionBindings(MandelbrotInputActions.PanRight,
            InputBinding.ForKey(InputKey.D), InputBinding.ForKey(InputKey.Right));
        input.SetActionBindings(MandelbrotInputActions.PanUp,
            InputBinding.ForKey(InputKey.W), InputBinding.ForKey(InputKey.Up));
        input.SetActionBindings(MandelbrotInputActions.PanDown,
            InputBinding.ForKey(InputKey.S), InputBinding.ForKey(InputKey.Down));
        input.SetActionBindings(MandelbrotInputActions.MoreIterations,
            InputBinding.ForKey(InputKey.Equal), InputBinding.ForKey(InputKey.PageUp));
        input.SetActionBindings(MandelbrotInputActions.FewerIterations,
            InputBinding.ForKey(InputKey.Minus), InputBinding.ForKey(InputKey.PageDown));
        input.SetActionBindings(MandelbrotInputActions.NextPalette, InputBinding.ForKey(InputKey.C));
        input.SetActionBindings(MandelbrotInputActions.Reset, InputBinding.ForKey(InputKey.R));

        Reality.Scenes.RegisterScene(ExplorerScene, () => new MandelbrotSceneLogic(input));
        Reality.Scenes.LoadScene(ExplorerScene);
        Reality.MainCamera.OrthographicSize = 1.0;
    }

    public override void Update(double dt)
    {
    }
}

internal static class MandelbrotInputActions
{
    internal const string PanLeft = "mandelbrot-pan-left";
    internal const string PanRight = "mandelbrot-pan-right";
    internal const string PanUp = "mandelbrot-pan-up";
    internal const string PanDown = "mandelbrot-pan-down";
    internal const string MoreIterations = "mandelbrot-more-iterations";
    internal const string FewerIterations = "mandelbrot-fewer-iterations";
    internal const string NextPalette = "mandelbrot-next-palette";
    internal const string Reset = "mandelbrot-reset";
}
