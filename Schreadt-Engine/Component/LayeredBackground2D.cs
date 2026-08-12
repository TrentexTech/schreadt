using Schreadt_Engine.Core;
using Silk.NET.Maths;

namespace Schreadt_Engine.Component;

/// <summary>
/// Composes backgrounds in back-to-front order. Each child is rendered with
/// its own parallax factor and origin.
/// </summary>
public sealed class LayeredBackground2D : IBackground2D, IReadOnlyList<IBackground2D>
{
    private readonly List<IBackground2D> _layers = [];

    public LayeredBackground2D()
    {
    }

    public LayeredBackground2D(IEnumerable<IBackground2D> layers)
    {
        ArgumentNullException.ThrowIfNull(layers);
        foreach (var layer in layers) Add(layer);
    }

    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Layer parallax is absolute relative to the scene camera, so the
    /// composite itself uses the neutral world-space factor.
    /// </summary>
    public double ParallaxFactor => 1.0;

    public Vector2D<double> ParallaxOrigin => Vector2D<double>.Zero;

    public IReadOnlyList<IBackground2D> Layers => this;

    public int Count => _layers.Count;

    public IBackground2D this[int index] => _layers[index];

    public T Add<T>(T layer) where T : IBackground2D
    {
        ValidateNewLayer(layer);
        _layers.Add(layer);
        return layer;
    }

    public T Insert<T>(int index, T layer) where T : IBackground2D
    {
        ValidateNewLayer(layer);
        _layers.Insert(index, layer);
        return layer;
    }

    public bool Remove(IBackground2D layer)
    {
        ArgumentNullException.ThrowIfNull(layer);
        return _layers.Remove(layer);
    }

    public void Clear() => _layers.Clear();

    public IEnumerator<IBackground2D> GetEnumerator() => _layers.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    public void Render(IBackgroundRenderContext2D context)
    {
        ArgumentNullException.ThrowIfNull(context);
        foreach (var layer in _layers.ToArray()) context.RenderBackground(layer);
    }

    private void ValidateNewLayer(IBackground2D layer)
    {
        ArgumentNullException.ThrowIfNull(layer);
        if (ReferenceEquals(layer, this))
            throw new InvalidOperationException("A layered background cannot contain itself.");
        if (_layers.Any(existing => ReferenceEquals(existing, layer)))
            throw new InvalidOperationException("The background is already present in this layered background.");
        if (layer is LayeredBackground2D composite && composite.Contains(this))
            throw new InvalidOperationException("Adding the background would create a background hierarchy cycle.");
    }

    private bool Contains(LayeredBackground2D candidate)
    {
        foreach (var layer in _layers)
        {
            if (ReferenceEquals(layer, candidate)) return true;
            if (layer is LayeredBackground2D composite && composite.Contains(candidate)) return true;
        }

        return false;
    }
}
