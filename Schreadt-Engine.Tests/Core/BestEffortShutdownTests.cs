using Schreadt_Engine.Core;

namespace Schreadt_Engine.Tests.Core;

public sealed class BestEffortShutdownTests
{
    [Fact]
    public void EveryStageRunsInOrderWhenEachOneFails()
    {
        var attempted = new List<string>();
        var failures = BestEffortShutdown.Run(CreateFailingStages(attempted, 5));

        Assert.Equal(["stage-1", "stage-2", "stage-3", "stage-4", "stage-5"], attempted);
        Assert.Equal(5, failures.Count);
        Assert.Equal(attempted, failures.Select(failure => failure.Stage));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public void FailureAtAnyWindowStage_DoesNotPreventLaterCleanup(int failingStage)
    {
        var attempted = new List<int>();
        var stages = Enumerable.Range(0, 9)
            .Select(index => new ShutdownStage($"stage-{index}", () =>
            {
                attempted.Add(index);
                if (index == failingStage) throw new InjectedShutdownException(index);
            }));

        var failures = BestEffortShutdown.Run(stages);

        Assert.Equal(Enumerable.Range(0, 9), attempted);
        var failure = Assert.Single(failures);
        Assert.Equal($"stage-{failingStage}", failure.Stage);
        Assert.IsType<InjectedShutdownException>(failure.Exception);
    }

    [Fact]
    public void OneFailure_IsRethrownWithoutReplacingItsTypeOrInstance()
    {
        var original = new InjectedShutdownException(2);
        var failures = BestEffortShutdown.Run(
        [
            new ShutdownStage("first", () => { }),
            new ShutdownStage("second", () => throw original),
            new ShutdownStage("third", () => { })
        ]);

        var thrown = Assert.Throws<InjectedShutdownException>(() => BestEffortShutdown.ThrowIfFailed(failures));

        Assert.Same(original, thrown);
    }

    [Fact]
    public void MultipleFailures_AreAggregatedAfterEveryStageRuns()
    {
        var first = new InjectedShutdownException(1);
        var second = new InjectedShutdownException(3);
        var attempted = new List<int>();
        var failures = BestEffortShutdown.Run(
        [
            new ShutdownStage("application", () => { attempted.Add(1); throw first; }),
            new ShutdownStage("renderer", () => attempted.Add(2)),
            new ShutdownStage("SDL", () => { attempted.Add(3); throw second; })
        ]);

        var aggregate = Assert.Throws<AggregateException>(() => BestEffortShutdown.ThrowIfFailed(failures));

        Assert.Equal([1, 2, 3], attempted);
        Assert.Equal([first, second], aggregate.InnerExceptions);
        Assert.Contains("application, SDL", aggregate.Message);
    }

    [Fact]
    public void EmptyOrSuccessfulSequence_DoesNotThrow()
    {
        BestEffortShutdown.ThrowIfFailed(BestEffortShutdown.Run([]));
        BestEffortShutdown.ThrowIfFailed(BestEffortShutdown.Run(
        [
            new ShutdownStage("first", () => { }),
            new ShutdownStage("second", () => { })
        ]));
    }

    private static IEnumerable<ShutdownStage> CreateFailingStages(List<string> attempted, int count)
    {
        return Enumerable.Range(1, count).Select(index =>
            new ShutdownStage($"stage-{index}", () =>
            {
                var name = $"stage-{index}";
                attempted.Add(name);
                throw new InjectedShutdownException(index);
            }));
    }

    private sealed class InjectedShutdownException(int stage) : Exception($"Failure at stage {stage}.");
}
