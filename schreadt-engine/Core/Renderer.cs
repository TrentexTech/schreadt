using Schreadt_Engine.Component;
using Silk.NET.Maths;
using Silk.NET.OpenGL;

namespace Schreadt_Engine.Core;

public sealed class Renderer : IDisposable
{
    private const int CircleSegmentCount = 96;

    private const string VertexShaderSource = """
        #version 330 core

        layout (location = 0) in vec2 aPosition;

        uniform vec2 uCenter;
        uniform vec2 uScale;

        void main()
        {
            gl_Position = vec4((aPosition * uScale) + uCenter, 0.0, 1.0);
        }
        """;

    private const string FragmentShaderSource = """
        #version 330 core

        out vec4 fragmentColor;

        uniform vec4 uColor;

        void main()
        {
            fragmentColor = uColor;
        }
        """;

    private readonly GL _gl;
    private readonly uint _circleVertexArray;
    private readonly uint _circleVertexBuffer;
    private readonly int _circleVertexCount;
    private readonly uint _shaderProgram;
    private readonly int _centerUniform;
    private readonly int _scaleUniform;
    private readonly int _colorUniform;

    private int _framebufferWidth = 1;
    private int _framebufferHeight = 1;
    private bool _disposed;

    public Renderer(GL gl)
    {
        _gl = gl;

        _shaderProgram = CreateShaderProgram();
        _centerUniform = _gl.GetUniformLocation(_shaderProgram, "uCenter");
        _scaleUniform = _gl.GetUniformLocation(_shaderProgram, "uScale");
        _colorUniform = _gl.GetUniformLocation(_shaderProgram, "uColor");

        (_circleVertexArray, _circleVertexBuffer, _circleVertexCount) = CreateCircleMesh();

        _gl.ClearColor(0.055f, 0.065f, 0.09f, 1.0f);
        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
    }

    public void Render(Camera camera, GameObject obj)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _gl.Clear(ClearBufferMask.ColorBufferBit);
        obj.Render(this);
    }

    public void Resize(int width, int height)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _framebufferWidth = Math.Max(width, 1);
        _framebufferHeight = Math.Max(height, 1);
        _gl.Viewport(new Vector2D<int>(0, 0), new Vector2D<int>(_framebufferWidth, _framebufferHeight));
    }

    public void DrawCircle(Vector2D<double> center, double radius, Vector4D<float> color)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (radius <= 0) return;

        var aspectCorrection = (float)_framebufferHeight / _framebufferWidth;
        var xScale = (float)radius * aspectCorrection;
        var yScale = (float)radius;

        _gl.UseProgram(_shaderProgram);
        _gl.Uniform2(_centerUniform, (float)center.X, (float)center.Y);
        _gl.Uniform2(_scaleUniform, xScale, yScale);
        _gl.Uniform4(_colorUniform, color.X, color.Y, color.Z, color.W);

        _gl.BindVertexArray(_circleVertexArray);
        _gl.DrawArrays(PrimitiveType.TriangleFan, 0, (uint)_circleVertexCount);
        _gl.BindVertexArray(0);
    }

    public void Dispose()
    {
        if (_disposed) return;

        _gl.DeleteBuffer(_circleVertexBuffer);
        _gl.DeleteVertexArray(_circleVertexArray);
        _gl.DeleteProgram(_shaderProgram);
        _gl.Dispose();
        _disposed = true;
    }

    private unsafe (uint VertexArray, uint VertexBuffer, int VertexCount) CreateCircleMesh()
    {
        var vertexCount = CircleSegmentCount + 2;
        var vertices = new float[vertexCount * 2];

        for (var segment = 0; segment <= CircleSegmentCount; segment++)
        {
            var angle = segment * Math.Tau / CircleSegmentCount;
            var vertexIndex = (segment + 1) * 2;
            vertices[vertexIndex] = (float)Math.Cos(angle);
            vertices[vertexIndex + 1] = (float)Math.Sin(angle);
        }

        var vertexArray = _gl.GenVertexArray();
        var vertexBuffer = _gl.GenBuffer();

        _gl.BindVertexArray(vertexArray);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, vertexBuffer);

        fixed (float* vertexData = vertices)
        {
            _gl.BufferData(
                BufferTargetARB.ArrayBuffer,
                (nuint)(vertices.Length * sizeof(float)),
                vertexData,
                BufferUsageARB.StaticDraw);
        }

        _gl.EnableVertexAttribArray(0);
        _gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 2 * sizeof(float), (void*)0);

        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
        _gl.BindVertexArray(0);

        return (vertexArray, vertexBuffer, vertexCount);
    }

    private uint CreateShaderProgram()
    {
        var vertexShader = CompileShader(ShaderType.VertexShader, VertexShaderSource);
        var fragmentShader = CompileShader(ShaderType.FragmentShader, FragmentShaderSource);
        var program = _gl.CreateProgram();

        _gl.AttachShader(program, vertexShader);
        _gl.AttachShader(program, fragmentShader);
        _gl.LinkProgram(program);
        _gl.GetProgram(program, ProgramPropertyARB.LinkStatus, out var linkStatus);

        _gl.DetachShader(program, vertexShader);
        _gl.DetachShader(program, fragmentShader);
        _gl.DeleteShader(vertexShader);
        _gl.DeleteShader(fragmentShader);

        if (linkStatus != 0) return program;

        var infoLog = _gl.GetProgramInfoLog(program);
        _gl.DeleteProgram(program);
        throw new InvalidOperationException($"Could not link the circle shader: {infoLog}");
    }

    private uint CompileShader(ShaderType type, string source)
    {
        var shader = _gl.CreateShader(type);
        _gl.ShaderSource(shader, source);
        _gl.CompileShader(shader);
        _gl.GetShader(shader, ShaderParameterName.CompileStatus, out var compileStatus);

        if (compileStatus != 0) return shader;

        var infoLog = _gl.GetShaderInfoLog(shader);
        _gl.DeleteShader(shader);
        throw new InvalidOperationException($"Could not compile the {type} shader: {infoLog}");
    }

}
