using Schreadt_Engine.Component.Logic;
using Schreadt_Engine.Core;
using Schreadt_Engine.Gui;
using Silk.NET.Maths;

namespace Mandelbrot_Explorer.Logic;

internal sealed class MandelbrotSceneLogic(IInputState input) : SceneLogic
{
    private MandelbrotCanvas _canvas = null!;
    private GuiLabel _stats = null!;

    public override void Init()
    {
        Scene.Background = null;
        _canvas = new MandelbrotCanvas();
        _canvas.Changed += UpdateStats;
        Scene.AddChild(_canvas);

        var help = Scene.Gui.AddPanel();
        help.Position = new Vector2D<float>(12, 12);
        help.Padding = 8;
        help.Spacing = 5;
        help.BackgroundColor = new Vector4D<float>(0.015f, 0.02f, 0.055f, 0.9f);
        var title = help.AddLabel("MANDELBROT EXPLORER");
        title.Color = new Vector4D<float>(0.35f, 0.93f, 1.0f, 1.0f);
        help.AddLabel("WHEEL: ZOOM AT CURSOR\nWASD/ARROWS: PAN   +/-: DETAIL\nC: PALETTE   R: RESET\n1-4: FRACTAL LANDMARKS").Scale = 1.15f;
        _stats = help.AddLabel(string.Empty);
        _stats.Scale = 1.15f;
        _stats.Color = new Vector4D<float>(1.0f, 0.82f, 0.25f, 1.0f);
        help.AddButton("RESET VIEW").Clicked += (_, _) => _canvas.Reset();
        help.AddButton("NEXT PALETTE").Clicked += (_, _) => _canvas.CyclePalette();
        UpdateStats();
    }

    public override void Update(double dt)
    {
        const double panStep = 0.12;
        if (input.WasActionPressed(MandelbrotInputActions.PanLeft)) _canvas.Pan(-panStep, 0.0);
        if (input.WasActionPressed(MandelbrotInputActions.PanRight)) _canvas.Pan(panStep, 0.0);
        if (input.WasActionPressed(MandelbrotInputActions.PanUp)) _canvas.Pan(0.0, panStep);
        if (input.WasActionPressed(MandelbrotInputActions.PanDown)) _canvas.Pan(0.0, -panStep);
        if (input.WasActionPressed(MandelbrotInputActions.MoreIterations)) _canvas.ChangeIterations(32);
        if (input.WasActionPressed(MandelbrotInputActions.FewerIterations)) _canvas.ChangeIterations(-32);
        if (input.WasActionPressed(MandelbrotInputActions.NextPalette)) _canvas.CyclePalette();
        if (input.WasActionPressed(MandelbrotInputActions.Reset)) _canvas.Reset();

        if (input.ScrollDelta.Y != 0.0f)
        {
            var zoomFactor = Math.Pow(0.78, input.ScrollDelta.Y);
            _canvas.ZoomAt(input.MouseViewportPosition, zoomFactor);
        }

        if (input.WasKeyPressed(InputKey.Number1)) _canvas.LoadLandmark(0);
        if (input.WasKeyPressed(InputKey.Number2)) _canvas.LoadLandmark(1);
        if (input.WasKeyPressed(InputKey.Number3)) _canvas.LoadLandmark(2);
        if (input.WasKeyPressed(InputKey.Number4)) _canvas.LoadLandmark(3);
    }

    private void UpdateStats()
    {
        if (_stats is null) return;
        var view = _canvas.View;
        var renderState = _canvas.IsGenerating ? "RENDERING" : "READY";
        _stats.Text = $"CENTER {view.CenterX:G9}, {view.CenterY:G9}\n" +
                      $"WIDTH {view.Width:G6}   ITER {view.MaxIterations}\n" +
                      $"PALETTE {_canvas.PaletteName}   {renderState}";
    }

    public override void Shutdown()
    {
        if (_canvas is not null) _canvas.Changed -= UpdateStats;
    }
}
