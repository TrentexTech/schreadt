using Schreadt_Engine.Collision;

namespace Schreadt_Engine.Tests.Collision;

public sealed class RigidBody2DTests
{
    [Fact]
    public void BodyType_RejectsUndefinedEnumValueWithoutChangingState()
    {
        var body = new RigidBody2D { BodyType = CollisionBodyType2D.Dynamic };

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            body.BodyType = (CollisionBodyType2D)999);

        Assert.Equal("value", exception.ParamName);
        Assert.Equal(CollisionBodyType2D.Dynamic, body.BodyType);
    }
}
