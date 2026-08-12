using System.Runtime.ExceptionServices;

namespace Schreadt_Engine.Core;

internal readonly record struct ShutdownStage
{
    internal ShutdownStage(string name, Action execute)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(execute);
        Name = name;
        Execute = execute;
    }

    internal string Name { get; }

    internal Action Execute { get; }
}

internal readonly record struct ShutdownFailure(string Stage, Exception Exception);

internal static class BestEffortShutdown
{
    internal static IReadOnlyList<ShutdownFailure> Run(IEnumerable<ShutdownStage> stages)
    {
        ArgumentNullException.ThrowIfNull(stages);
        List<ShutdownFailure>? failures = null;

        foreach (var stage in stages)
        {
            try
            {
                stage.Execute();
            }
            catch (Exception exception)
            {
                (failures ??= []).Add(new ShutdownFailure(stage.Name, exception));
            }
        }

        return failures ?? [];
    }

    internal static void ThrowIfFailed(IReadOnlyList<ShutdownFailure> failures)
    {
        ArgumentNullException.ThrowIfNull(failures);
        if (failures.Count == 0) return;
        if (failures.Count == 1)
        {
            ExceptionDispatchInfo.Capture(failures[0].Exception).Throw();
            return;
        }

        throw new AggregateException(
            $"Window shutdown failed in {failures.Count} stages: " +
            string.Join(", ", failures.Select(failure => failure.Stage)) + ".",
            failures.Select(failure => failure.Exception));
    }
}
