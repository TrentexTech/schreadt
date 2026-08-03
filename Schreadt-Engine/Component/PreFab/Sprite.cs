using Schreadt_Engine.Component.Logic;
using Schreadt_Engine.Core;
using Schreadt_Engine.Animation;
using Silk.NET.Maths;

namespace Schreadt_Engine.Component.PreFab;

public class Sprite : Actor, ISpriteRegionTarget
{
    public string ImageAssetId { get; }
    public Vector2D<double> Size { get; set; } = new(1.0, 1.0);
    public double RotationRadians { get; set; }
    public Vector4D<float> Tint { get; set; } = Vector4D<float>.One;
    public TextureRegion Region { get; set; } = TextureRegion.Full;
    public TextureSampling Sampling { get; set; } = TextureSampling.Linear;

    public Sprite(string imageAssetId, ActorLogic? actorLogic = null) : base(actorLogic)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imageAssetId);
        ImageAssetId = imageAssetId;
    }

    protected override void OnRender(IRenderContext2D renderer)
    {
        renderer.DrawSprite(
            ImageAssetId,
            Position,
            Size,
            Tint,
            RotationRadians,
            Region,
            Sampling);
    }
}
