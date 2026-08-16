using Example_Game.Logic.scenes;
using Schreadt_Engine.Component;
using Schreadt_Engine.Component.Logic;
using Silk.NET.Maths;

namespace Schreadt_Engine.Tests.Examples;

public sealed class PhysicsShowcaseTests
{
    [Fact]
    public void FoundrySeesaw_UsesLimitedRegisteredJointAndRespondsToOffCenterLoad()
    {
        var scene = new Scene("physics-showcase", new EmptySceneLogic());
        scene.Collisions.Gravity = Vector2D<double>.Zero;
        var seesaw = new FoundrySeesaw(
            new Vector2D<double>(2.5, 0.18),
            Vector2D<double>.Zero);
        scene.AddChild(seesaw);

        Assert.Same(seesaw.Joint, Assert.Single(scene.Collisions.Joints));
        Assert.True(seesaw.Joint.LimitsEnabled);
        Assert.Equal(-0.34, seesaw.Joint.LowerAngle);
        Assert.Equal(0.34, seesaw.Joint.UpperAngle);

        seesaw.Body.AddImpulseAtPoint(
            new Vector2D<double>(0.0, -1.0),
            new Vector2D<double>(-1.0, 0.0));
        for (var step = 0; step < 240; step++) scene.Collisions.Step(1.0 / 120.0);

        Assert.InRange(seesaw.RotationRadians, 0.05, 0.36);
    }

    private sealed class EmptySceneLogic : SceneLogic
    {
        public override void Init()
        {
        }

        public override void Update(double dt)
        {
        }
    }
}
