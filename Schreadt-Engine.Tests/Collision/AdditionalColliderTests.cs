using Schreadt_Engine.Collision;
using Schreadt_Engine.Component;
using Schreadt_Engine.Component.Logic;
using Schreadt_Engine.Component.PreFab;
using Silk.NET.Maths;

namespace Schreadt_Engine.Tests.Collision;

public sealed class AdditionalColliderTests
{
    [Fact]
    public void BoxBox_UsesAxisWithLeastPenetration()
    {
        var first = CreateBox(new Vector2D<double>(2.0, 2.0), Vector2D<double>.Zero);
        var second = CreateBox(new Vector2D<double>(2.0, 2.0), new Vector2D<double>(1.5, 0.25));
        var narrowPhase = new BoxBoxNarrowPhase2D();

        var collided = narrowPhase.TryCollide(first, second, out var result);

        Assert.True(collided);
        Assert.Equal(new Vector2D<double>(1.0, 0.0), result.Normal);
        Assert.Equal(0.5, result.Penetration, 10);
    }

    [Fact]
    public void BoxBox_ReturnsFalseForSeparatedBoxes()
    {
        var first = CreateBox(Vector2D<double>.One, Vector2D<double>.Zero);
        var second = CreateBox(Vector2D<double>.One, new Vector2D<double>(1.01, 0.0));

        var collided = new BoxBoxNarrowPhase2D().TryCollide(first, second, out _);

        Assert.False(collided);
    }

    [Fact]
    public void CircleBox_DetectsCornerOverlap()
    {
        var circle = CreateCircle(0.5, new Vector2D<double>(1.3, 1.3));
        var box = CreateBox(new Vector2D<double>(2.0, 2.0), Vector2D<double>.Zero);

        var collided = new CircleBoxNarrowPhase2D().TryCollide(circle, box, out var result);

        Assert.True(collided);
        var expectedComponent = -1.0 / Math.Sqrt(2.0);
        Assert.Equal(expectedComponent, result.Normal.X, 10);
        Assert.Equal(expectedComponent, result.Normal.Y, 10);
        Assert.Equal(0.5 - Math.Sqrt(0.18), result.Penetration, 10);
    }

    [Fact]
    public void CircleBox_InsideBox_UsesNearestExitFace()
    {
        var circle = CreateCircle(0.25, new Vector2D<double>(0.8, 0.0));
        var box = CreateBox(new Vector2D<double>(2.0, 2.0), Vector2D<double>.Zero);

        var collided = new CircleBoxNarrowPhase2D().TryCollide(circle, box, out var result);

        Assert.True(collided);
        Assert.Equal(new Vector2D<double>(-1.0, 0.0), result.Normal);
        Assert.Equal(0.45, result.Penetration, 10);
    }

    [Fact]
    public void CircleBox_ReturnsFalseWhenCircleMissesCorner()
    {
        var circle = CreateCircle(0.4, new Vector2D<double>(1.3, 1.3));
        var box = CreateBox(new Vector2D<double>(2.0, 2.0), Vector2D<double>.Zero);

        var collided = new CircleBoxNarrowPhase2D().TryCollide(circle, box, out _);

        Assert.False(collided);
    }

    [Fact]
    public void BoxCollider_RejectsInvalidGeometry()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AxisAlignedBoxCollider2D(Vector2D<double>.Zero));

        var collider = new AxisAlignedBoxCollider2D(Vector2D<double>.One);
        Assert.Throws<ArgumentOutOfRangeException>(() => collider.Offset = new Vector2D<double>(double.NaN, 0.0));
    }

    [Fact]
    public void CollisionWorld_UsesCircleBoxHandlerWhenBoxWasAddedFirst()
    {
        var scene = new Scene("test", new EmptySceneLogic());
        var boxObject = new Rectangle2D { Size = Vector2D<double>.One };
        var box = boxObject.AddComponent(new AxisAlignedBoxCollider2D(boxObject.Size));
        var circleObject = new Circle { Position = new Vector2D<double>(0.7, 0.0), Radius = 0.3 };
        var circle = circleObject.AddComponent(new CircleCollider2D(circleObject.Radius));
        CollisionContact2D? contact = null;
        box.CollisionEntered += entered => contact = entered;

        scene.AddChild(boxObject);
        scene.AddChild(circleObject);
        scene.Collisions.Step(0.0);

        Assert.True(contact.HasValue);
        Assert.Same(circle, contact.Value.Other);
        Assert.Equal(new Vector2D<double>(1.0, 0.0), contact.Value.Normal);
        Assert.Equal(0.1, contact.Value.Penetration, 10);
    }

    private static AxisAlignedBoxCollider2D CreateBox(Vector2D<double> size, Vector2D<double> position)
    {
        var rectangle = new Rectangle2D { Position = position, Size = size };
        return rectangle.AddComponent(new AxisAlignedBoxCollider2D(size));
    }

    private static CircleCollider2D CreateCircle(double radius, Vector2D<double> position)
    {
        var circle = new Circle { Position = position, Radius = radius };
        return circle.AddComponent(new CircleCollider2D(radius));
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
