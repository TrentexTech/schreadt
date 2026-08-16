using Schreadt_Engine.Collision;
using Schreadt_Engine.Component;
using Schreadt_Engine.Core;
using Schreadt_Engine.Gui;
using Silk.NET.Maths;

namespace Example_Game.Logic;

internal interface IInteractable2D
{
    string InteractionPrompt { get; }

    bool CanInteract(PlayerAvatar player);

    void Interact(PlayerAvatar player);
}

internal sealed class PlayerInteractionBehavior : ActorBehavior
{
    private const double QueryOffset = 0.48;
    private const double QueryRadius = 0.42;
    private static readonly CollisionQueryFilter2D QueryFilter = new(
        ExampleCollisionLayers.InteractableOnlyMask,
        includeTriggers: true);

    private readonly IInputState _input;
    private readonly GuiPanel _promptPanel;
    private readonly GuiLabel _promptLabel;
    private readonly List<Collider2D> _queryResults = [];
    private PlayerAvatar _player = null!;
    private int _facingDirection = 1;

    internal IInteractable2D? CurrentTarget { get; private set; }

    internal int FacingDirection => _facingDirection;

    internal PlayerInteractionBehavior(
        IInputState input,
        GuiPanel promptPanel,
        GuiLabel promptLabel)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(promptPanel);
        ArgumentNullException.ThrowIfNull(promptLabel);
        _input = input;
        _promptPanel = promptPanel;
        _promptLabel = promptLabel;
        _promptPanel.Visible = false;
    }

    public override void Init()
    {
        _player = (PlayerAvatar)Actor;
        var screens = Actor.Scene?.Screens
            ?? throw new InvalidOperationException("The player must belong to a scene before interaction initializes.");
        screens.ScreenPushed += HandleScreenPushed;
        screens.TransitionStarted += HandleScreenTransitionStarted;
        ClearTarget();
    }

    public override void Update(double dt)
    {
        UpdateFacingDirection();
        if (!_input.Available || IsInteractionBlocked())
        {
            ClearTarget();
            return;
        }

        SetTarget(FindTarget());
        if (CurrentTarget is null || !_input.WasActionPressed(ExampleInputActions.Interact)) return;

        var target = CurrentTarget;
        if (!target.CanInteract(_player))
        {
            ClearTarget();
            return;
        }

        target.Interact(_player);
        if (target.CanInteract(_player)) ShowPrompt(target.InteractionPrompt);
        else ClearTarget();
    }

    public override void Shutdown()
    {
        if (Actor.Scene is { } scene)
        {
            scene.Screens.ScreenPushed -= HandleScreenPushed;
            scene.Screens.TransitionStarted -= HandleScreenTransitionStarted;
        }

        ClearTarget();
    }

    protected override void OnAttached()
    {
        base.OnAttached();
        if (Owner is not PlayerAvatar)
            throw new InvalidOperationException("PlayerInteractionBehavior can only be attached to a player avatar.");
    }

    private IInteractable2D? FindTarget()
    {
        var scene = Actor.Scene;
        if (scene is null) return null;

        var queryCenter = Actor.Position + new Vector2D<double>(_facingDirection * QueryOffset, 0.0);
        scene.Collisions.OverlapCircle(queryCenter, QueryRadius, _queryResults, QueryFilter);

        IInteractable2D? bestTarget = null;
        var bestDistanceSquared = double.PositiveInfinity;
        foreach (var collider in _queryResults)
        {
            if (collider.Owner is not IInteractable2D candidate || !candidate.CanInteract(_player)) continue;

            var offset = collider.Center - Actor.Position;
            var distanceSquared = offset.X * offset.X + offset.Y * offset.Y;
            if (distanceSquared >= bestDistanceSquared) continue;

            bestDistanceSquared = distanceSquared;
            bestTarget = candidate;
        }

        return bestTarget;
    }

    private bool IsInteractionBlocked()
    {
        var screens = Actor.Scene?.Screens;
        return screens is null ||
               screens.IsTransitioning ||
               screens.Top?.IsModal == true;
    }

    private void UpdateFacingDirection()
    {
        var left = _input.IsActionDown(ExampleInputActions.MoveLeft);
        var right = _input.IsActionDown(ExampleInputActions.MoveRight);
        if (left == right) return;
        _facingDirection = right ? 1 : -1;
    }

    private void SetTarget(IInteractable2D? target)
    {
        CurrentTarget = target;
        if (target is null) ClearTarget();
        else ShowPrompt(target.InteractionPrompt);
    }

    private void ShowPrompt(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            ClearTarget();
            return;
        }

        _promptLabel.Text = prompt;
        _promptPanel.Visible = true;
    }

    private void ClearTarget()
    {
        CurrentTarget = null;
        _promptLabel.Text = string.Empty;
        _promptPanel.Visible = false;
    }

    private void HandleScreenPushed(GuiScreen screen)
    {
        if (screen.IsModal) ClearTarget();
    }

    private void HandleScreenTransitionStarted() => ClearTarget();
}
