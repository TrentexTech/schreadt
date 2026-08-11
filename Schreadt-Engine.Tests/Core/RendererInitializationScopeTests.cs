using Schreadt_Engine.Core;

namespace Schreadt_Engine.Tests.Core;

public sealed class RendererInitializationScopeTests
{
    [Fact]
    public void LaterStageFailure_RollsBackShaderMeshAndTextureResourcesInReverseOrder()
    {
        var deleted = new List<(RendererResourceKind Kind, uint Handle)>();
        var initialization = new RendererInitializationScope((kind, handle) => deleted.Add((kind, handle)));

        initialization.Track(RendererResourceKind.Program, 1);
        initialization.Track(RendererResourceKind.VertexArray, 2);
        initialization.Track(RendererResourceKind.Buffer, 3);
        initialization.Track(RendererResourceKind.Program, 4);
        initialization.Track(RendererResourceKind.Texture, 5);

        var failures = initialization.Rollback();

        Assert.Empty(failures);
        Assert.Equal(
        [
            (RendererResourceKind.Texture, 5U),
            (RendererResourceKind.Program, 4U),
            (RendererResourceKind.Buffer, 3U),
            (RendererResourceKind.VertexArray, 2U),
            (RendererResourceKind.Program, 1U)
        ],
            deleted);
    }

    [Fact]
    public void CompileOrLinkFailure_RollsBackTemporaryShadersAndProgram()
    {
        var deleted = new List<(RendererResourceKind Kind, uint Handle)>();
        var initialization = new RendererInitializationScope((kind, handle) => deleted.Add((kind, handle)));

        initialization.Track(RendererResourceKind.Shader, 10);
        initialization.Track(RendererResourceKind.Shader, 11);
        initialization.Track(RendererResourceKind.Program, 12);

        initialization.Rollback();

        Assert.Equal(
        [
            (RendererResourceKind.Program, 12U),
            (RendererResourceKind.Shader, 11U),
            (RendererResourceKind.Shader, 10U)
        ],
            deleted);
    }

    [Fact]
    public void SuccessfullyDeletedTemporaryShader_IsNotDeletedAgainDuringRollback()
    {
        var deleted = new List<(RendererResourceKind Kind, uint Handle)>();
        var initialization = new RendererInitializationScope((kind, handle) => deleted.Add((kind, handle)));
        initialization.Track(RendererResourceKind.Shader, 20);
        initialization.Track(RendererResourceKind.Program, 21);

        initialization.Delete(RendererResourceKind.Shader, 20);
        initialization.Rollback();

        Assert.Equal(
        [
            (RendererResourceKind.Shader, 20U),
            (RendererResourceKind.Program, 21U)
        ],
            deleted);
    }

    [Fact]
    public void FailedTemporaryShaderDeletion_RemainsTrackedForRollback()
    {
        var attempts = 0;
        var initialization = new RendererInitializationScope((_, _) =>
        {
            attempts++;
            if (attempts == 1) throw new InvalidOperationException("injected delete failure");
        });
        initialization.Track(RendererResourceKind.Shader, 20);

        Assert.Throws<InvalidOperationException>(() =>
            initialization.Delete(RendererResourceKind.Shader, 20));
        var failures = initialization.Rollback();

        Assert.Empty(failures);
        Assert.Equal(2, attempts);
    }

    [Fact]
    public void Rollback_ContinuesAfterAResourceDeletionFails()
    {
        var attempted = new List<uint>();
        var initialization = new RendererInitializationScope((_, handle) =>
        {
            attempted.Add(handle);
            if (handle == 2) throw new InvalidOperationException("injected delete failure");
        });
        initialization.Track(RendererResourceKind.Program, 1);
        initialization.Track(RendererResourceKind.Buffer, 2);
        initialization.Track(RendererResourceKind.Texture, 3);

        var failures = initialization.Rollback();

        Assert.Equal([3U, 2U, 1U], attempted);
        var failure = Assert.Single(failures);
        Assert.Equal("injected delete failure", failure.Message);
    }

    [Fact]
    public void CompletedInitialization_DoesNotDeleteRendererOwnedResources()
    {
        var deleted = new List<uint>();
        var initialization = new RendererInitializationScope((_, handle) => deleted.Add(handle));
        initialization.Track(RendererResourceKind.Program, 1);
        initialization.Track(RendererResourceKind.Texture, 2);

        initialization.Complete();
        var failures = initialization.Rollback();

        Assert.Empty(failures);
        Assert.Empty(deleted);
    }
}
