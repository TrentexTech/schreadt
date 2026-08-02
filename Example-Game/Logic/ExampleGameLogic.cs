using Schreadt_Engine.Component.Logic;
using Schreadt_Engine.Core;

namespace Example_Game.Logic;

public class ExampleGameLogic : GameLogic
{
    private const double DefaultOrthographicSize = 1.25;
    private const double MinimumOrthographicSize = 0.25;
    private const double MaximumOrthographicSize = 8.0;
    private const double ZoomFactorPerScrollStep = 0.85;

    public override void Update(double dt)
    {
        var scroll = State.Input.ScrollDelta.Y;
        if (scroll == 0)
        {
            return;
        }

        var camera = Reality.MainCamera;
        var requestedSize = camera.OrthographicSize * Math.Pow(ZoomFactorPerScrollStep, scroll);
        camera.OrthographicSize = Math.Clamp(
            requestedSize,
            MinimumOrthographicSize,
            MaximumOrthographicSize);
    }

    public override void Init()
    {
        Reality.MainCamera.OrthographicSize = DefaultOrthographicSize;
    }
}
