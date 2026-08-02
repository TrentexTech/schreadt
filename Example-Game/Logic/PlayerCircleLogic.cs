using Schreadt_Engine.Component.Logic;
using Schreadt_Engine.Core;
using Silk.NET.Input;
using Silk.NET.Maths;

namespace Example_Game.Logic;

public sealed class PlayerCircleLogic : ActorLogic
{
    private const double MovementSpeed = 1.25;

    public override void Init()
    {
    }

    public override void Update(double dt)
    {
        var movement = Vector2D<double>.Zero;
        var input = State.Input;

        if (input.IsKeyDown(Key.W) || input.IsKeyDown(Key.Up)) movement.Y += 1.0;
        if (input.IsKeyDown(Key.S) || input.IsKeyDown(Key.Down)) movement.Y -= 1.0;
        if (input.IsKeyDown(Key.A) || input.IsKeyDown(Key.Left)) movement.X -= 1.0;
        if (input.IsKeyDown(Key.D) || input.IsKeyDown(Key.Right)) movement.X += 1.0;

        var length = Math.Sqrt(movement.X * movement.X + movement.Y * movement.Y);
        if (length > 0) Actor.Move(movement * (MovementSpeed * dt / length));

        if (input.WasMouseButtonPressed(MouseButton.Left))
        {
            Actor.Position = State.CurrentReality.MainCamera.ViewportToWorldPoint(
                input.MouseViewportPosition,
                input.ViewportAspectRatio);
        }
    }
}
