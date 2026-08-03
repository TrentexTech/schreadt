using Schreadt_Engine.Component;
using Silk.NET.Maths;

namespace Schreadt_Engine.Tests.Core;

public sealed class CameraSystemTests
{
    [Fact]
    public void FollowController_SnapsOnInitAndHonorsDeadZone()
    {
        var target = new TestGameObject { Position = new Vector2D<double>(2.0, 3.0) };
        var camera = new Camera();
        camera.SetController(new FollowCameraController2D(target)
        {
            SmoothTime = 0.0,
            DeadZone = Vector2D<double>.One
        });
        camera.Init();
        Assert.Equal(target.Position, camera.Position);

        target.Position += new Vector2D<double>(0.4, -0.4);
        camera.Update(0.1);
        Assert.Equal(new Vector2D<double>(2.0, 3.0), camera.Position);

        target.Position = new Vector2D<double>(3.0, 4.0);
        camera.Update(0.1);
        Assert.Equal(new Vector2D<double>(2.5, 3.5), camera.Position);
    }

    [Fact]
    public void FollowController_SmoothsUsingFrameRateIndependentResponse()
    {
        var target = new TestGameObject { Position = new Vector2D<double>(10.0, 0.0) };
        var camera = new Camera();
        camera.SetController(new FollowCameraController2D(target)
        {
            SnapOnInit = false,
            SmoothTime = 1.0
        });
        camera.Init();

        camera.Update(1.0);

        Assert.Equal(10.0 * (1.0 - Math.Exp(-1.0)), camera.Position.X, 10);
        Assert.Equal(0.0, camera.Position.Y);
    }

    [Fact]
    public void FollowController_ClampsWholeViewInsideWorldBounds()
    {
        var target = new TestGameObject { Position = new Vector2D<double>(10.0, 10.0) };
        var camera = new Camera { OrthographicSize = 1.0 };
        camera.WorldToViewportPoint(Vector2D<double>.Zero, aspectRatio: 2.0);
        var controller = camera.SetController(new FollowCameraController2D(target)
        {
            WorldBounds = new CameraBounds2D(
                new Vector2D<double>(-5.0, -3.0),
                new Vector2D<double>(5.0, 3.0))
        });
        camera.Init();

        Assert.Equal(new Vector2D<double>(3.0, 2.0), camera.Position);

        camera.OrthographicSize = 10.0;
        controller.SnapToTarget();
        Assert.Equal(Vector2D<double>.Zero, camera.Position);
    }

    [Fact]
    public void FollowController_SupportsTargetOffsetAndRetargeting()
    {
        var first = new TestGameObject { Position = new Vector2D<double>(1.0, 2.0) };
        var second = new TestGameObject { Position = new Vector2D<double>(-2.0, 4.0) };
        var camera = new Camera();
        var controller = camera.SetController(new FollowCameraController2D(first)
        {
            TargetOffset = new Vector2D<double>(0.5, -0.5)
        });
        camera.Init();
        Assert.Equal(new Vector2D<double>(1.5, 1.5), camera.Position);

        controller.Target = second;
        controller.SnapToTarget();
        Assert.Equal(new Vector2D<double>(-1.5, 3.5), camera.Position);
    }

    [Fact]
    public void CameraShake_OffsetsRenderedViewWithoutMovingLogicalCamera()
    {
        var logicalPosition = new Vector2D<double>(5.0, 6.0);
        var camera = new Camera
        {
            Position = logicalPosition,
            RotationRadians = 0.2
        };
        var shake = camera.AddComponent(new CameraShake2D());
        camera.Init();

        shake.Shake(0.5, 0.2, 0.1, 10.0);

        Assert.True(shake.IsShaking);
        Assert.Equal(logicalPosition, camera.Position);
        Assert.NotEqual(camera.Position, camera.RenderPosition);
        Assert.NotEqual(camera.RotationRadians, camera.RenderRotationRadians);

        camera.Update(0.5);

        Assert.False(shake.IsShaking);
        Assert.Equal(camera.Position, camera.RenderPosition);
        Assert.Equal(camera.RotationRadians, camera.RenderRotationRadians);
        Assert.Equal(0.0, shake.RemainingTime);
    }

    [Fact]
    public void CameraShake_IsExclusiveAndCleansUpWhenRemoved()
    {
        var camera = new Camera();
        var shake = camera.AddComponent(new CameraShake2D());
        camera.Init();
        shake.Shake(1.0, 0.1);

        Assert.Throws<InvalidOperationException>(() => camera.AddComponent(new CameraShake2D()));
        Assert.True(camera.RemoveComponent(shake));
        Assert.Equal(camera.Position, camera.RenderPosition);

        var ordinaryObject = new TestGameObject();
        Assert.Throws<InvalidOperationException>(() => ordinaryObject.AddComponent(new CameraShake2D()));
    }

    [Fact]
    public void CameraConfiguration_RejectsInvalidValues()
    {
        var target = new TestGameObject();
        var controller = new FollowCameraController2D(target);

        Assert.Throws<ArgumentOutOfRangeException>(() => controller.SmoothTime = -1.0);
        Assert.Throws<ArgumentOutOfRangeException>(() => controller.DeadZone = new Vector2D<double>(-1.0, 0.0));
        Assert.Throws<ArgumentException>(() => new CameraBounds2D(Vector2D<double>.One, Vector2D<double>.Zero));

        var shake = new Camera().AddComponent(new CameraShake2D());
        Assert.Throws<ArgumentOutOfRangeException>(() => shake.Shake(0.0, 1.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => shake.Shake(1.0, -1.0));
    }

    private sealed class TestGameObject : GameObject
    {
    }
}
