using Schreadt_Engine.Component.Logic;
using Schreadt_Engine.Core;
using Silk.NET.Maths;

namespace Example_Game.Logic;

public sealed class PlayerCircleLogic : ActorLogic
{
    private const double MovementSpeed = 1.25;
    private readonly IInputState? _input;

    private IInputState Input => _input ?? State.Input;

    public PlayerCircleLogic(IInputState? input = null)
    {
        _input = input;
    }

    public override void Init()
    {
    }

    public override void Update(double dt)
    {
        var movement = Vector2D<double>.Zero;
        var input = Input;

        if (input.IsActionDown(ExampleInputActions.MoveUp)) movement.Y += 1.0;
        if (input.IsActionDown(ExampleInputActions.MoveDown)) movement.Y -= 1.0;
        if (input.IsActionDown(ExampleInputActions.MoveLeft)) movement.X -= 1.0;
        if (input.IsActionDown(ExampleInputActions.MoveRight)) movement.X += 1.0;

        var length = Math.Sqrt(movement.X * movement.X + movement.Y * movement.Y);
        if (length > 0) Actor.Move(movement * (MovementSpeed * dt / length));

        if (input.WasActionPressed(ExampleInputActions.MoveToPointer) && !State.Gui.IsPointerCaptured)
        {
            Actor.Position = State.CurrentReality.MainCamera.ViewportToWorldPoint(
                input.MouseViewportPosition,
                input.ViewportAspectRatio);
        }
    }
}
