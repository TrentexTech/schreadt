using Schreadt_Engine.Component;

namespace Example_Game.Logic;

public sealed class FollowTargetCameraLogic : ICameraLogic
{
    private readonly GameObject _target;
    private Camera? _camera;

    public FollowTargetCameraLogic(GameObject target)
    {
        _target = target;
    }

    public void Init(Camera camera)
    {
        _camera = camera;
        FollowTarget();
    }

    public void Update(double dt)
    {
        FollowTarget();
    }

    private void FollowTarget()
    {
        if (_camera is not null) _camera.Position = _target.Position;
    }
}
