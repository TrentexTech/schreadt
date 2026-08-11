using Schreadt_Engine.Asset;
using Schreadt_Engine.Gui;

namespace Schreadt_Engine.Core;

public static class State
{
    private static readonly object Sync = new();
    private static StateSnapshot? _current;

    [Obsolete("Use IEngineContext.LaunchArgs from the engine lifecycle context.")]
    public static string[] LaunchArgs => Current?.Context.LaunchArgs.ToArray() ?? [];

    [Obsolete("Use the Reality or IEngineContext supplied by the engine lifecycle.")]
    public static Reality CurrentReality => RequireCurrent().Reality;

    [Obsolete("Use IEngineContext.Input from the engine lifecycle context.")]
    public static IInputService Input => RequireCurrent().Context.Input;

    [Obsolete("Use IEngineContext.Gui from the engine lifecycle context.")]
    public static GuiSystem Gui => RequireCurrent().Context.Gui;

    [Obsolete("Use IEngineContext.Assets from the engine lifecycle context.")]
    public static AssetCatalog Assets => RequireCurrent().Context.Assets as AssetCatalog
        ?? throw new InvalidOperationException("The compatibility facade requires an AssetCatalog instance.");

    [Obsolete("Use IEngineContext.Window from the engine lifecycle context.")]
    public static IWindowController Window => RequireCurrent().Context.Window;

    [Obsolete("Use IEngineContext.Runtime from the engine lifecycle context.")]
    public static RuntimeController Runtime => RequireCurrent().Context.Runtime;

    internal static IEngineContext? CurrentContext => Current?.Context;

    internal static void Publish(IEngineContext context, Reality reality)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(reality);

        lock (Sync)
        {
            if (_current is not null)
                throw new InvalidOperationException("An engine context is already published for this process.");

            Volatile.Write(ref _current, new StateSnapshot(context, reality));
        }
    }

    internal static void Reset(IEngineContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        lock (Sync)
        {
            if (_current is not null && ReferenceEquals(_current.Context, context))
                Volatile.Write(ref _current, null);
        }
    }

    private static StateSnapshot? Current => Volatile.Read(ref _current);

    private static StateSnapshot RequireCurrent() => Current
        ?? throw new InvalidOperationException("The engine has not published an initialized context yet.");

    private sealed record StateSnapshot(IEngineContext Context, Reality Reality);
}
