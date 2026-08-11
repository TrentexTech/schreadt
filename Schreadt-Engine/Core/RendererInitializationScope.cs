namespace Schreadt_Engine.Core;

internal enum RendererResourceKind
{
    Shader,
    Program,
    Buffer,
    VertexArray,
    Texture
}

internal sealed class RendererInitializationScope(
    Action<RendererResourceKind, uint> deleteResource)
{
    private readonly List<Resource> _resources = [];
    private bool _completed;

    internal uint Track(RendererResourceKind kind, uint handle)
    {
        if (_completed) throw new InvalidOperationException("Renderer initialization is already complete.");
        if (handle != 0) _resources.Add(new Resource(kind, handle));
        return handle;
    }

    internal void Delete(RendererResourceKind kind, uint handle)
    {
        if (handle == 0) return;

        var index = _resources.FindLastIndex(resource => resource.Kind == kind && resource.Handle == handle);
        if (index < 0)
            throw new InvalidOperationException($"The {kind} handle {handle} is not tracked by this initialization.");

        deleteResource(kind, handle);
        _resources.RemoveAt(index);
    }

    internal IReadOnlyList<Exception> Rollback()
    {
        if (_completed) return [];

        List<Exception>? failures = null;
        for (var index = _resources.Count - 1; index >= 0; index--)
        {
            var resource = _resources[index];
            try
            {
                deleteResource(resource.Kind, resource.Handle);
            }
            catch (Exception exception)
            {
                (failures ??= []).Add(exception);
            }
        }

        _resources.Clear();
        _completed = true;
        return failures ?? [];
    }

    internal void Complete()
    {
        if (_completed) return;
        _resources.Clear();
        _completed = true;
    }

    private readonly record struct Resource(RendererResourceKind Kind, uint Handle);
}
