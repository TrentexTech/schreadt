using Schreadt_Engine.Asset;
using Schreadt_Engine.Component;
using Schreadt_Engine.Gui;
using Silk.NET.Maths;
using Silk.NET.OpenGL;

namespace Schreadt_Engine.Core;

public sealed class Renderer : IRenderer2D
{
    private const int CircleSegmentCount = 96;
    private const int MaximumGridLineCount = 2048;
    private static readonly Vector4D<float> SceneClearColor = new(0.055f, 0.065f, 0.09f, 1.0f);
    private static readonly Vector2D<double>[] RectangleVertices =
    [
        new(-0.5, 0.5),
        new(-0.5, -0.5),
        new(0.5, -0.5),
        new(0.5, 0.5)
    ];

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

    private const string SpriteVertexShaderSource = """
        #version 330 core

        layout (location = 0) in vec2 aPosition;
        layout (location = 1) in vec2 aTextureCoordinate;

        uniform vec2 uCenter;
        uniform vec2 uAxisX;
        uniform vec2 uAxisY;
        uniform vec4 uTextureRegion;

        out vec2 textureCoordinate;

        void main()
        {
            vec2 position = uCenter + aPosition.x * uAxisX + aPosition.y * uAxisY;
            gl_Position = vec4(position, 0.0, 1.0);
            textureCoordinate = mix(uTextureRegion.xy, uTextureRegion.zw, aTextureCoordinate);
        }
        """;

    private const string SpriteFragmentShaderSource = """
        #version 330 core

        in vec2 textureCoordinate;
        out vec4 fragmentColor;

        uniform sampler2D uTexture;
        uniform vec4 uTint;

        void main()
        {
            fragmentColor = texture(uTexture, textureCoordinate) * uTint;
        }
        """;

    private readonly GL _gl;
    private readonly IAssetProvider? _assets;
    private readonly double _targetAspectRatio;
    private readonly Dictionary<string, Texture2D> _textures = new(StringComparer.Ordinal);
    private readonly uint _circleVertexArray;
    private readonly uint _circleVertexBuffer;
    private readonly int _circleVertexCount;
    private readonly uint _lineVertexArray;
    private readonly uint _lineVertexBuffer;
    private readonly uint _shaderProgram;
    private readonly int _centerUniform;
    private readonly int _scaleUniform;
    private readonly int _colorUniform;
    private readonly uint _spriteVertexArray;
    private readonly uint _spriteVertexBuffer;
    private readonly uint _spriteShaderProgram;
    private readonly int _spriteCenterUniform;
    private readonly int _spriteAxisXUniform;
    private readonly int _spriteAxisYUniform;
    private readonly int _spriteRegionUniform;
    private readonly int _spriteTintUniform;
    private readonly uint _pixelTexture;

    private int _framebufferWidth = 1;
    private int _framebufferHeight = 1;
    private int _viewportX;
    private int _viewportY;
    private int _viewportWidth = 1;
    private int _viewportHeight = 1;
    private CameraView _cameraView;
    private bool _renderingFrame;
    private bool _disposed;
    private TextureSampling? _pixelTextureSampling;
    private int _pixelBufferWidth;
    private int _pixelBufferHeight;

    public Vector2D<int> ViewportOffset => new(_viewportX, _viewportY);

    public Vector2D<int> ViewportSize => new(_viewportWidth, _viewportHeight);

    public Renderer(GL gl, IAssetProvider? assets = null, double targetAspectRatio = 16.0 / 9.0)
    {
        if (!double.IsFinite(targetAspectRatio) || targetAspectRatio <= 0.0)
            throw new ArgumentOutOfRangeException(
                nameof(targetAspectRatio),
                "The target aspect ratio must be finite and greater than zero.");

        _gl = gl;
        _assets = assets;
        _targetAspectRatio = targetAspectRatio;

        _shaderProgram = CreateShaderProgram();
        _centerUniform = _gl.GetUniformLocation(_shaderProgram, "uCenter");
        _scaleUniform = _gl.GetUniformLocation(_shaderProgram, "uScale");
        _colorUniform = _gl.GetUniformLocation(_shaderProgram, "uColor");

        (_circleVertexArray, _circleVertexBuffer, _circleVertexCount) = CreateCircleMesh();
        (_lineVertexArray, _lineVertexBuffer) = CreateLineMesh();

        _spriteShaderProgram = CreateShaderProgram(SpriteVertexShaderSource, SpriteFragmentShaderSource, "sprite");
        _spriteCenterUniform = _gl.GetUniformLocation(_spriteShaderProgram, "uCenter");
        _spriteAxisXUniform = _gl.GetUniformLocation(_spriteShaderProgram, "uAxisX");
        _spriteAxisYUniform = _gl.GetUniformLocation(_spriteShaderProgram, "uAxisY");
        _spriteRegionUniform = _gl.GetUniformLocation(_spriteShaderProgram, "uTextureRegion");
        _spriteTintUniform = _gl.GetUniformLocation(_spriteShaderProgram, "uTint");
        (_spriteVertexArray, _spriteVertexBuffer) = CreateSpriteMesh();

        _gl.UseProgram(_spriteShaderProgram);
        _gl.Uniform1(_gl.GetUniformLocation(_spriteShaderProgram, "uTexture"), 0);

        _pixelTexture = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, _pixelTexture);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
        _gl.BindTexture(TextureTarget.Texture2D, 0);

        _gl.ClearColor(0.0f, 0.0f, 0.0f, 1.0f);
        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        EngineLog.Information(
            $"OpenGL renderer initialized; target aspect ratio: {_targetAspectRatio:F4}; " +
            $"asset catalog: {(_assets is null ? "none" : $"{_assets.Count} asset(s)")}.",
            "Renderer");
    }

    public void Render(Camera camera, GameObject obj, GuiSystem? gui = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(obj);

        _cameraView = camera.CreateView((double)_viewportWidth / _viewportHeight);
        _renderingFrame = true;

        try
        {
            _gl.Clear(ClearBufferMask.ColorBufferBit);
            DrawScreenRectangle(Vector2D<float>.Zero, new Vector2D<float>(_viewportWidth, _viewportHeight), SceneClearColor);
            if (obj is Scene { Background.Enabled: true } scene) DrawGrid(scene.Background);
            obj.Render(this);
            if (obj is Scene debugScene) debugScene.Collisions.DrawDiagnostics(this);
            gui?.Render(this);
        }
        finally
        {
            _renderingFrame = false;
        }
    }

    public void Resize(int width, int height)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _framebufferWidth = Math.Max(width, 1);
        _framebufferHeight = Math.Max(height, 1);
        var (offset, size) = CalculateViewport(_framebufferWidth, _framebufferHeight, _targetAspectRatio);
        _viewportX = offset.X;
        _viewportY = offset.Y;
        _viewportWidth = size.X;
        _viewportHeight = size.Y;

        var openGlY = _framebufferHeight - _viewportY - _viewportHeight;
        _gl.Viewport(new Vector2D<int>(_viewportX, openGlY), new Vector2D<int>(_viewportWidth, _viewportHeight));
        EngineLog.Debug(
            $"Renderer resized: framebuffer {_framebufferWidth}x{_framebufferHeight}; " +
            $"viewport {_viewportWidth}x{_viewportHeight} at ({_viewportX}, {_viewportY}).",
            "Renderer");
    }

    internal static (Vector2D<int> Offset, Vector2D<int> Size) CalculateViewport(
        int framebufferWidth,
        int framebufferHeight,
        double targetAspectRatio)
    {
        if (framebufferWidth <= 0 || framebufferHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(framebufferWidth), "Framebuffer dimensions must be positive.");
        if (!double.IsFinite(targetAspectRatio) || targetAspectRatio <= 0.0)
            throw new ArgumentOutOfRangeException(nameof(targetAspectRatio));

        var framebufferAspectRatio = (double)framebufferWidth / framebufferHeight;
        int viewportWidth;
        int viewportHeight;

        if (framebufferAspectRatio > targetAspectRatio)
        {
            viewportHeight = framebufferHeight;
            viewportWidth = Math.Clamp((int)Math.Round(viewportHeight * targetAspectRatio), 1, framebufferWidth);
        }
        else
        {
            viewportWidth = framebufferWidth;
            viewportHeight = Math.Clamp((int)Math.Round(viewportWidth / targetAspectRatio), 1, framebufferHeight);
        }

        return (
            new Vector2D<int>(
                (framebufferWidth - viewportWidth) / 2,
                (framebufferHeight - viewportHeight) / 2),
            new Vector2D<int>(viewportWidth, viewportHeight));
    }

    public void DrawCircle(Vector2D<double> center, double radius, Vector4D<float> color)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (radius <= 0) return;
        if (!_renderingFrame) throw new InvalidOperationException("Draw calls must occur while a camera is rendering a frame.");

        var projectedCenter = _cameraView.WorldToNormalizedDevicePoint(center);
        var projectedScale = _cameraView.WorldRadiusToNormalizedDeviceScale(radius);

        _gl.UseProgram(_shaderProgram);
        _gl.Uniform2(_centerUniform, (float)projectedCenter.X, (float)projectedCenter.Y);
        _gl.Uniform2(_scaleUniform, (float)projectedScale.X, (float)projectedScale.Y);
        _gl.Uniform4(_colorUniform, color.X, color.Y, color.Z, color.W);

        _gl.BindVertexArray(_circleVertexArray);
        _gl.DrawArrays(PrimitiveType.TriangleFan, 0, (uint)_circleVertexCount);
        _gl.BindVertexArray(0);
    }

    public void DrawRectangle(
        Vector2D<double> center,
        Vector2D<double> size,
        Vector4D<float> color,
        double rotationRadians = 0.0)
    {
        DrawPolygon(center, RectangleVertices, size, rotationRadians, color);
    }

    public void DrawPolygon(
        Vector2D<double> center,
        IReadOnlyList<Vector2D<double>> localVertices,
        Vector2D<double> scale,
        double rotationRadians,
        Vector4D<float> color)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_renderingFrame) throw new InvalidOperationException("Draw calls must occur while a camera is rendering a frame.");
        if (!double.IsFinite(center.X) || !double.IsFinite(center.Y))
            throw new ArgumentOutOfRangeException(nameof(center), "Shape position must be finite.");
        if (!double.IsFinite(scale.X) || !double.IsFinite(scale.Y) || scale.X <= 0.0 || scale.Y <= 0.0)
            throw new ArgumentOutOfRangeException(nameof(scale), "Shape scale must be finite and positive.");
        if (!double.IsFinite(rotationRadians))
            throw new ArgumentOutOfRangeException(nameof(rotationRadians), "Shape rotation must be finite.");

        ConvexPolygon2D.Validate(localVertices, nameof(localVertices));

        var cosine = Math.Cos(rotationRadians);
        var sine = Math.Sin(rotationRadians);
        var projected = new Vector2D<double>[localVertices.Count];
        for (var index = 0; index < localVertices.Count; index++)
        {
            var local = localVertices[index];
            var scaledX = local.X * scale.X;
            var scaledY = local.Y * scale.Y;
            var world = center + new Vector2D<double>(
                scaledX * cosine - scaledY * sine,
                scaledX * sine + scaledY * cosine);
            projected[index] = _cameraView.WorldToNormalizedDevicePoint(world);
        }

        var vertices = new List<float>((projected.Length - 2) * 6);
        for (var index = 1; index < projected.Length - 1; index++)
        {
            AddVertex(vertices, projected[0]);
            AddVertex(vertices, projected[index]);
            AddVertex(vertices, projected[index + 1]);
        }

        DrawDynamicBatch(vertices, color, PrimitiveType.Triangles);
    }

    public void DrawSprite(
        string imageAssetId,
        Vector2D<double> center,
        Vector2D<double> size,
        Vector4D<float> tint,
        double rotationRadians = 0.0,
        TextureRegion? region = null,
        TextureSampling sampling = TextureSampling.Linear)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_renderingFrame) throw new InvalidOperationException("Draw calls must occur while a camera is rendering a frame.");
        if (!double.IsFinite(center.X) || !double.IsFinite(center.Y))
            throw new ArgumentOutOfRangeException(nameof(center), "Sprite position must be finite.");
        if (!double.IsFinite(size.X) || !double.IsFinite(size.Y) || size.X <= 0.0 || size.Y <= 0.0)
            throw new ArgumentOutOfRangeException(nameof(size), "Sprite size must be finite and positive.");
        if (!double.IsFinite(rotationRadians))
            throw new ArgumentOutOfRangeException(nameof(rotationRadians), "Sprite rotation must be finite.");

        var sourceRegion = region ?? TextureRegion.Full;
        sourceRegion.Validate();
        var texture = GetTexture(imageAssetId);
        SetTextureSampling(texture, sampling);

        var cosine = Math.Cos(rotationRadians);
        var sine = Math.Sin(rotationRadians);
        var halfXAxis = new Vector2D<double>(cosine * size.X * 0.5, sine * size.X * 0.5);
        var halfYAxis = new Vector2D<double>(-sine * size.Y * 0.5, cosine * size.Y * 0.5);
        var projectedCenter = _cameraView.WorldToNormalizedDevicePoint(center);
        var projectedXAxis = _cameraView.WorldToNormalizedDevicePoint(center + halfXAxis) - projectedCenter;
        var projectedYAxis = _cameraView.WorldToNormalizedDevicePoint(center + halfYAxis) - projectedCenter;

        _gl.UseProgram(_spriteShaderProgram);
        _gl.Uniform2(_spriteCenterUniform, (float)projectedCenter.X, (float)projectedCenter.Y);
        _gl.Uniform2(_spriteAxisXUniform, (float)projectedXAxis.X, (float)projectedXAxis.Y);
        _gl.Uniform2(_spriteAxisYUniform, (float)projectedYAxis.X, (float)projectedYAxis.Y);
        _gl.Uniform4(_spriteRegionUniform, sourceRegion.Left, sourceRegion.Top, sourceRegion.Right, sourceRegion.Bottom);
        _gl.Uniform4(_spriteTintUniform, tint.X, tint.Y, tint.Z, tint.W);
        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.Texture2D, texture.Handle);
        _gl.BindVertexArray(_spriteVertexArray);
        _gl.DrawArrays(PrimitiveType.Triangles, 0, 6);
        _gl.BindVertexArray(0);
        _gl.BindTexture(TextureTarget.Texture2D, 0);
    }

    public Texture2D GetTexture(string imageAssetId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_assets is null)
            throw new InvalidOperationException("This renderer was created without an asset catalog.");

        var image = _assets.Get<ImageAsset>(imageAssetId);
        if (_textures.TryGetValue(image.Id, out var cached)) return cached;

        var texture = UploadTexture(image);
        _textures.Add(image.Id, texture);
        EngineLog.Debug(
            $"Uploaded texture '{image.Id}' ({image.Width}x{image.Height}) to the GPU.",
            "Renderer");
        return texture;
    }

    public void DrawText(
        string text,
        Vector2D<float> position,
        float scale,
        Vector4D<float> color,
        Vector4D<float> backgroundColor,
        float padding = 0.0f)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(text);
        if (!_renderingFrame) throw new InvalidOperationException("GUI drawing must occur while rendering a frame.");
        if (!float.IsFinite(position.X) || !float.IsFinite(position.Y))
            throw new ArgumentOutOfRangeException(nameof(position), "Text position must be finite.");
        if (!float.IsFinite(scale) || scale <= 0.0f)
            throw new ArgumentOutOfRangeException(nameof(scale), "Text scale must be finite and greater than zero.");
        if (!float.IsFinite(padding) || padding < 0.0f)
            throw new ArgumentOutOfRangeException(nameof(padding), "Text padding must be finite and non-negative.");

        var lines = text.Replace("\r", string.Empty).Split('\n');
        var longestLine = lines.Max(line => line.Length);
        var textWidth = Math.Max(0.0f, (longestLine * BitmapFont5x7.CharacterAdvance - 1) * scale);
        var textHeight = Math.Max(0.0f, (lines.Length * BitmapFont5x7.LineAdvance - 1) * scale);
        var textX = position.X + padding;
        var textY = position.Y + padding;

        if (backgroundColor.W > 0)
        {
            var backgroundVertices = new List<float>(12);
            AddScreenQuad(
                backgroundVertices,
                position.X,
                position.Y,
                textWidth + padding * 2.0f,
                textHeight + padding * 2.0f);
            DrawDynamicBatch(backgroundVertices, backgroundColor, PrimitiveType.Triangles);
        }

        var glyphVertices = new List<float>();

        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            var line = lines[lineIndex];

            for (var characterIndex = 0; characterIndex < line.Length; characterIndex++)
            {
                var glyph = BitmapFont5x7.GetGlyph(line[characterIndex]);
                var glyphX = textX + characterIndex * BitmapFont5x7.CharacterAdvance * scale;
                var glyphY = textY + lineIndex * BitmapFont5x7.LineAdvance * scale;

                for (var row = 0; row < BitmapFont5x7.GlyphHeight; row++)
                {
                    for (var column = 0; column < BitmapFont5x7.GlyphWidth; column++)
                    {
                        var bit = 1 << (BitmapFont5x7.GlyphWidth - column - 1);
                        if ((glyph[row] & bit) == 0) continue;

                        AddScreenQuad(
                            glyphVertices,
                            glyphX + column * scale,
                            glyphY + row * scale,
                            scale,
                            scale);
                    }
                }
            }
        }

        DrawDynamicBatch(glyphVertices, color, PrimitiveType.Triangles);
    }

    public void DrawScreenRectangle(
        Vector2D<float> position,
        Vector2D<float> size,
        Vector4D<float> color)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_renderingFrame) throw new InvalidOperationException("GUI drawing must occur while rendering a frame.");
        if (!float.IsFinite(position.X) || !float.IsFinite(position.Y))
            throw new ArgumentOutOfRangeException(nameof(position), "Screen position must be finite.");
        if (!float.IsFinite(size.X) || !float.IsFinite(size.Y) || size.X < 0.0f || size.Y < 0.0f)
            throw new ArgumentOutOfRangeException(nameof(size), "Screen size must be finite and non-negative.");

        var vertices = new List<float>(12);
        AddScreenQuad(vertices, position.X, position.Y, size.X, size.Y);
        DrawDynamicBatch(vertices, color, PrimitiveType.Triangles);
    }

    public unsafe void DrawScreenPixels(
        ReadOnlySpan<byte> rgbaPixels,
        int pixelWidth,
        int pixelHeight,
        TextureSampling sampling = TextureSampling.Nearest)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_renderingFrame) throw new InvalidOperationException("Pixel buffers must be drawn while rendering a frame.");
        if (pixelWidth <= 0) throw new ArgumentOutOfRangeException(nameof(pixelWidth));
        if (pixelHeight <= 0) throw new ArgumentOutOfRangeException(nameof(pixelHeight));

        int expectedLength;
        try
        {
            expectedLength = checked(pixelWidth * pixelHeight * 4);
        }
        catch (OverflowException)
        {
            throw new ArgumentOutOfRangeException(nameof(pixelWidth), "Pixel buffer dimensions are too large.");
        }

        if (rgbaPixels.Length != expectedLength)
            throw new ArgumentException(
                $"The pixel buffer must contain exactly {expectedLength} RGBA bytes.",
                nameof(rgbaPixels));

        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.Texture2D, _pixelTexture);
        SetPixelTextureSampling(sampling);
        _gl.PixelStore(PixelStoreParameter.UnpackAlignment, 1);
        if (_pixelBufferWidth != pixelWidth || _pixelBufferHeight != pixelHeight)
        {
            _pixelBufferWidth = pixelWidth;
            _pixelBufferHeight = pixelHeight;
            EngineLog.Debug($"Pixel-buffer source changed to {pixelWidth}x{pixelHeight} RGBA.", "Renderer");
        }

        fixed (byte* data = rgbaPixels)
        {
            _gl.TexImage2D(
                TextureTarget.Texture2D,
                0,
                InternalFormat.Rgba8,
                (uint)pixelWidth,
                (uint)pixelHeight,
                0,
                PixelFormat.Rgba,
                PixelType.UnsignedByte,
                data);
        }

        _gl.UseProgram(_spriteShaderProgram);
        _gl.Uniform2(_spriteCenterUniform, 0.0f, 0.0f);
        _gl.Uniform2(_spriteAxisXUniform, 1.0f, 0.0f);
        _gl.Uniform2(_spriteAxisYUniform, 0.0f, 1.0f);
        _gl.Uniform4(_spriteRegionUniform, 0.0f, 0.0f, 1.0f, 1.0f);
        _gl.Uniform4(_spriteTintUniform, 1.0f, 1.0f, 1.0f, 1.0f);
        _gl.BindVertexArray(_spriteVertexArray);
        _gl.DrawArrays(PrimitiveType.Triangles, 0, 6);
        _gl.BindVertexArray(0);
        _gl.BindTexture(TextureTarget.Texture2D, 0);
    }

    public void Dispose()
    {
        if (_disposed) return;

        foreach (var texture in _textures.Values) _gl.DeleteTexture(texture.Handle);
        _textures.Clear();
        _gl.DeleteTexture(_pixelTexture);
        _gl.DeleteBuffer(_spriteVertexBuffer);
        _gl.DeleteVertexArray(_spriteVertexArray);
        _gl.DeleteProgram(_spriteShaderProgram);
        _gl.DeleteBuffer(_circleVertexBuffer);
        _gl.DeleteVertexArray(_circleVertexArray);
        _gl.DeleteBuffer(_lineVertexBuffer);
        _gl.DeleteVertexArray(_lineVertexArray);
        _gl.DeleteProgram(_shaderProgram);
        _gl.Dispose();
        _disposed = true;
        EngineLog.Information("Renderer resources disposed.", "Renderer");
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

    private unsafe (uint VertexArray, uint VertexBuffer) CreateLineMesh()
    {
        var vertexArray = _gl.GenVertexArray();
        var vertexBuffer = _gl.GenBuffer();
        var initialVertices = new float[4];

        _gl.BindVertexArray(vertexArray);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, vertexBuffer);

        fixed (float* vertexData = initialVertices)
        {
            _gl.BufferData(
                BufferTargetARB.ArrayBuffer,
                (nuint)(initialVertices.Length * sizeof(float)),
                vertexData,
                BufferUsageARB.DynamicDraw);
        }

        _gl.EnableVertexAttribArray(0);
        _gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 2 * sizeof(float), (void*)0);

        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
        _gl.BindVertexArray(0);

        return (vertexArray, vertexBuffer);
    }

    private unsafe (uint VertexArray, uint VertexBuffer) CreateSpriteMesh()
    {
        float[] vertices =
        [
            -1.0f,  1.0f, 0.0f, 0.0f,
            -1.0f, -1.0f, 0.0f, 1.0f,
             1.0f, -1.0f, 1.0f, 1.0f,
            -1.0f,  1.0f, 0.0f, 0.0f,
             1.0f, -1.0f, 1.0f, 1.0f,
             1.0f,  1.0f, 1.0f, 0.0f
        ];

        var vertexArray = _gl.GenVertexArray();
        var vertexBuffer = _gl.GenBuffer();
        _gl.BindVertexArray(vertexArray);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, vertexBuffer);

        fixed (float* data = vertices)
        {
            _gl.BufferData(
                BufferTargetARB.ArrayBuffer,
                (nuint)(vertices.Length * sizeof(float)),
                data,
                BufferUsageARB.StaticDraw);
        }

        const uint stride = 4 * sizeof(float);
        _gl.EnableVertexAttribArray(0);
        _gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, stride, (void*)0);
        _gl.EnableVertexAttribArray(1);
        _gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, stride, (void*)(2 * sizeof(float)));
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
        _gl.BindVertexArray(0);
        return (vertexArray, vertexBuffer);
    }

    private unsafe Texture2D UploadTexture(ImageAsset image)
    {
        var handle = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, handle);
        _gl.PixelStore(PixelStoreParameter.UnpackAlignment, 1);

        var pixels = image.Pixels.Span;
        fixed (byte* data = pixels)
        {
            _gl.TexImage2D(
                TextureTarget.Texture2D,
                0,
                InternalFormat.Rgba8,
                (uint)image.Width,
                (uint)image.Height,
                0,
                PixelFormat.Rgba,
                PixelType.UnsignedByte,
                data);
        }

        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
        _gl.BindTexture(TextureTarget.Texture2D, 0);
        return new Texture2D(handle, image.Id, image.Width, image.Height);
    }

    private void SetTextureSampling(Texture2D texture, TextureSampling sampling)
    {
        if (texture.CurrentSampling == sampling) return;

        var filter = sampling switch
        {
            TextureSampling.Nearest => (int)TextureMinFilter.Nearest,
            TextureSampling.Linear => (int)TextureMinFilter.Linear,
            _ => throw new ArgumentOutOfRangeException(nameof(sampling), sampling, "Unsupported texture sampling mode.")
        };

        _gl.BindTexture(TextureTarget.Texture2D, texture.Handle);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, filter);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, filter);
        texture.CurrentSampling = sampling;
    }

    private void SetPixelTextureSampling(TextureSampling sampling)
    {
        if (_pixelTextureSampling == sampling) return;

        var filter = sampling switch
        {
            TextureSampling.Nearest => (int)TextureMinFilter.Nearest,
            TextureSampling.Linear => (int)TextureMinFilter.Linear,
            _ => throw new ArgumentOutOfRangeException(nameof(sampling), sampling, "Unsupported texture sampling mode.")
        };

        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, filter);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, filter);
        _pixelTextureSampling = sampling;
    }

    private void DrawGrid(GridBackground2D grid)
    {
        var corners = new[]
        {
            _cameraView.NormalizedDeviceToWorldPoint(new Vector2D<double>(-1.0, -1.0)),
            _cameraView.NormalizedDeviceToWorldPoint(new Vector2D<double>(-1.0, 1.0)),
            _cameraView.NormalizedDeviceToWorldPoint(new Vector2D<double>(1.0, -1.0)),
            _cameraView.NormalizedDeviceToWorldPoint(new Vector2D<double>(1.0, 1.0))
        };
        var minimumX = corners.Min(point => point.X);
        var maximumX = corners.Max(point => point.X);
        var minimumY = corners.Min(point => point.Y);
        var maximumY = corners.Max(point => point.Y);
        var estimatedLineCount = (maximumX - minimumX + maximumY - minimumY) / grid.CellSize + 4.0;
        var indexStride = Math.Max(1L, (long)Math.Ceiling(estimatedLineCount / MaximumGridLineCount));
        var minimumXIndex = (long)Math.Floor(minimumX / grid.CellSize);
        var maximumXIndex = (long)Math.Ceiling(maximumX / grid.CellSize);
        var minimumYIndex = (long)Math.Floor(minimumY / grid.CellSize);
        var maximumYIndex = (long)Math.Ceiling(maximumY / grid.CellSize);
        var minorVertices = new List<float>();
        var majorVertices = new List<float>();
        var axisVertices = new List<float>();

        for (var index = FirstMultipleAtOrAbove(minimumXIndex, indexStride);
             index <= maximumXIndex;
             index += indexStride)
        {
            var x = index * grid.CellSize;
            AddGridLine(
                SelectGridBatch(index, grid.MajorLineEvery, minorVertices, majorVertices, axisVertices),
                new Vector2D<double>(x, minimumY),
                new Vector2D<double>(x, maximumY));
        }

        for (var index = FirstMultipleAtOrAbove(minimumYIndex, indexStride);
             index <= maximumYIndex;
             index += indexStride)
        {
            var y = index * grid.CellSize;
            AddGridLine(
                SelectGridBatch(index, grid.MajorLineEvery, minorVertices, majorVertices, axisVertices),
                new Vector2D<double>(minimumX, y),
                new Vector2D<double>(maximumX, y));
        }

        DrawLineBatch(minorVertices, grid.MinorLineColor);
        DrawLineBatch(majorVertices, grid.MajorLineColor);
        DrawLineBatch(axisVertices, grid.OriginAxisColor);
    }

    private void AddGridLine(List<float> vertices, Vector2D<double> start, Vector2D<double> end)
    {
        var projectedStart = _cameraView.WorldToNormalizedDevicePoint(start);
        var projectedEnd = _cameraView.WorldToNormalizedDevicePoint(end);

        vertices.Add((float)projectedStart.X);
        vertices.Add((float)projectedStart.Y);
        vertices.Add((float)projectedEnd.X);
        vertices.Add((float)projectedEnd.Y);
    }

    private static void AddVertex(List<float> vertices, Vector2D<double> vertex)
    {
        vertices.Add((float)vertex.X);
        vertices.Add((float)vertex.Y);
    }

    private void DrawLineBatch(List<float> vertices, Vector4D<float> color)
    {
        DrawDynamicBatch(vertices, color, PrimitiveType.Lines);
    }

    private void AddScreenQuad(List<float> vertices, float x, float y, float width, float height)
    {
        if (width <= 0 || height <= 0) return;

        var left = -1.0f + 2.0f * x / _viewportWidth;
        var right = -1.0f + 2.0f * (x + width) / _viewportWidth;
        var top = 1.0f - 2.0f * y / _viewportHeight;
        var bottom = 1.0f - 2.0f * (y + height) / _viewportHeight;

        vertices.Add(left);
        vertices.Add(top);
        vertices.Add(left);
        vertices.Add(bottom);
        vertices.Add(right);
        vertices.Add(bottom);

        vertices.Add(left);
        vertices.Add(top);
        vertices.Add(right);
        vertices.Add(bottom);
        vertices.Add(right);
        vertices.Add(top);
    }

    private unsafe void DrawDynamicBatch(
        List<float> vertices,
        Vector4D<float> color,
        PrimitiveType primitiveType)
    {
        if (vertices.Count == 0) return;

        var vertexData = vertices.ToArray();

        _gl.UseProgram(_shaderProgram);
        _gl.Uniform2(_centerUniform, 0.0f, 0.0f);
        _gl.Uniform2(_scaleUniform, 1.0f, 1.0f);
        _gl.Uniform4(_colorUniform, color.X, color.Y, color.Z, color.W);
        _gl.BindVertexArray(_lineVertexArray);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _lineVertexBuffer);

        fixed (float* data = vertexData)
        {
            _gl.BufferData(
                BufferTargetARB.ArrayBuffer,
                (nuint)(vertexData.Length * sizeof(float)),
                data,
                BufferUsageARB.DynamicDraw);
        }

        _gl.DrawArrays(primitiveType, 0, (uint)(vertexData.Length / 2));
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
        _gl.BindVertexArray(0);
    }

    private static List<float> SelectGridBatch(
        long lineIndex,
        int majorLineEvery,
        List<float> minorVertices,
        List<float> majorVertices,
        List<float> axisVertices)
    {
        if (lineIndex == 0) return axisVertices;
        return lineIndex % majorLineEvery == 0 ? majorVertices : minorVertices;
    }

    private static long FirstMultipleAtOrAbove(long value, long interval)
    {
        return (long)(Math.Ceiling(value / (double)interval) * interval);
    }

    private uint CreateShaderProgram()
    {
        return CreateShaderProgram(VertexShaderSource, FragmentShaderSource, "shape");
    }

    private uint CreateShaderProgram(string vertexSource, string fragmentSource, string label)
    {
        var vertexShader = CompileShader(ShaderType.VertexShader, vertexSource);
        var fragmentShader = CompileShader(ShaderType.FragmentShader, fragmentSource);
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
        throw new InvalidOperationException($"Could not link the {label} shader: {infoLog}");
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
