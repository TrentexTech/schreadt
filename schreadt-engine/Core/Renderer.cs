using Schreadt_Engine.Component;
using Silk.NET.OpenGL;

namespace Schreadt_Engine.Core;

public class Renderer
{
    private GL _gl;

    public Renderer(GL gl)
    {
        _gl = gl;
    }

    public void Render(Camera camera, GameObject obj)
    {
    }
}