using Schreadt_Engine.Component;

namespace Example_Game.Logic;

public sealed class FollowTargetCameraLogic : CameraController
{
    private readonly GameObject _target;

    public FollowTargetCameraLogic(GameObject target)
    {
        _target = target;
    }

    public override void Init()
    {
        FollowTarget();
    }

    public override void Update(double dt)
    {
        FollowTarget();
    }

    private void FollowTarget()
    {
        Camera.Position = _target.Position;
    }
}
