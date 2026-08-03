using Schreadt_Engine.Component.Logic;
using Schreadt_Engine.Core;

namespace Mandelbrot_Explorer.Logic;

internal sealed class MandelbrotGameLogic : GameLogic
{
    internal const string ExplorerScene = "mandelbrot-explorer";

    public override void Init()
    {
        State.Input.SetActionBindings(MandelbrotInputActions.PanLeft,
            InputBinding.ForKey(InputKey.A), InputBinding.ForKey(InputKey.Left));
        State.Input.SetActionBindings(MandelbrotInputActions.PanRight,
            InputBinding.ForKey(InputKey.D), InputBinding.ForKey(InputKey.Right));
        State.Input.SetActionBindings(MandelbrotInputActions.PanUp,
            InputBinding.ForKey(InputKey.W), InputBinding.ForKey(InputKey.Up));
        State.Input.SetActionBindings(MandelbrotInputActions.PanDown,
            InputBinding.ForKey(InputKey.S), InputBinding.ForKey(InputKey.Down));
        State.Input.SetActionBindings(MandelbrotInputActions.MoreIterations,
            InputBinding.ForKey(InputKey.Equal), InputBinding.ForKey(InputKey.PageUp));
        State.Input.SetActionBindings(MandelbrotInputActions.FewerIterations,
            InputBinding.ForKey(InputKey.Minus), InputBinding.ForKey(InputKey.PageDown));
        State.Input.SetActionBindings(MandelbrotInputActions.NextPalette, InputBinding.ForKey(InputKey.C));
        State.Input.SetActionBindings(MandelbrotInputActions.Reset, InputBinding.ForKey(InputKey.R));

        Reality.Scenes.RegisterScene(ExplorerScene, () => new MandelbrotSceneLogic(State.Input));
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
