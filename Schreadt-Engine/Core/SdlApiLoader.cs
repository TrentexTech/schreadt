using System.Runtime.InteropServices;
using Silk.NET.SDL;

namespace Schreadt_Engine.Core;

/// <summary>
/// Preloads SDL from .NET's native search directories so Silk's manual loader
/// can find a library extracted from a single-file application bundle.
/// </summary>
internal static class SdlApiLoader
{
    private static readonly object Sync = new();
    private static nint _nativeHandle;
    private static bool _preloadAttempted;

    internal static Sdl GetApi()
    {
        EnsureNativeLibraryLoaded();
        return Sdl.GetApi();
    }

    private static void EnsureNativeLibraryLoaded()
    {
        lock (Sync)
        {
            if (_preloadAttempted) return;
            _preloadAttempted = true;

            var assembly = typeof(SdlApiLoader).Assembly;
            foreach (var name in GetLibraryNames())
            {
                if (!NativeLibrary.TryLoad(
                        name,
                        assembly,
                        DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories,
                        out _nativeHandle))
                    continue;

                EngineLog.Debug($"Preloaded SDL native library as '{name}'.", "SDL");
                return;
            }

            var nativeSearchDirectories = AppContext.GetData("NATIVE_DLL_SEARCH_DIRECTORIES") as string;
            if (!string.IsNullOrWhiteSpace(nativeSearchDirectories))
            {
                foreach (var directory in nativeSearchDirectories.Split(
                             Path.PathSeparator,
                             StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    foreach (var name in GetLibraryFileNames())
                    {
                        var path = Path.Combine(directory, name);
                        if (!File.Exists(path) || !NativeLibrary.TryLoad(path, out _nativeHandle)) continue;
                        EngineLog.Debug($"Preloaded SDL native library from '{path}'.", "SDL");
                        return;
                    }
                }
            }

            EngineLog.Debug("SDL preload did not resolve a library; falling back to Silk's loader.", "SDL");
        }
    }

    private static IReadOnlyList<string> GetLibraryNames()
    {
        if (OperatingSystem.IsWindows()) return ["SDL2", "SDL2.dll"];
        if (OperatingSystem.IsMacOS()) return ["SDL2", "SDL2-2.0", "libSDL2-2.0.dylib", "libSDL2.dylib"];
        return ["SDL2", "SDL2-2.0", "libSDL2-2.0.so", "libSDL2-2.0.so.0", "libSDL2.so"];
    }

    private static IReadOnlyList<string> GetLibraryFileNames()
    {
        if (OperatingSystem.IsWindows()) return ["SDL2.dll"];
        if (OperatingSystem.IsMacOS()) return ["libSDL2-2.0.dylib", "libSDL2.dylib"];
        return ["libSDL2-2.0.so", "libSDL2-2.0.so.0", "libSDL2.so"];
    }
}

