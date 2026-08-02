using Schreadt_Engine.Component.Logic;

namespace Schreadt_Engine.Component;

public class Camera : GameObject
{
    private ICameraLogic? _cameraLogic;

    public void InitLogic(ICameraLogic logic)
    {
        _cameraLogic = logic;
        _cameraLogic.Init(this);
    }

    protected override void OnUpdate(double dt)
    {
        _cameraLogic?.Update(dt);
    }
}

public interface ICameraLogic : ILogic
{
    void Init(Camera camera);
}