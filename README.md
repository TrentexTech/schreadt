# Schreadt

Schreadt is an experimental 2D game engine written in C# on top of SDL2 and
OpenGL. The repository also contains two applications used to exercise the
engine:

- `Example-Game` is a multi-level 2D platformer demonstrating gameplay,
  physics, GUI screens, transitions, and other engine features.
- `Mandelbrot-Explorer` is an interactive Mandelbrot-set viewer demonstrating
  pixel rendering and asynchronous background generation.

The project is under active development. APIs and project structure may still
change.

## Requirements

- Windows or Linux.
- The .NET SDK selected by `global.json` (currently .NET SDK 10.0.100).
- A graphics driver capable of creating an OpenGL 3.3 core context.
- PowerShell 7 (`pwsh`) when using the publishing scripts.

SDK roll-forward is disabled, so another .NET 10 SDK version is not selected
automatically. Check the active version from the repository root:

```powershell
dotnet --version
```

## Build and test

Restore uses the committed NuGet lock files by default:

```powershell
dotnet restore Schreadt.slnx
dotnet build Schreadt.slnx --no-restore
dotnet test --solution Schreadt.slnx --no-restore
```

Use `--configuration Release` with the build and test commands to validate a
release build.

## Run the examples

After restoring dependencies, run either application from the repository root:

```powershell
dotnet run --project Example-Game/Example-Game.csproj --no-restore
dotnet run --project Mandelbrot-Explorer/Mandelbrot-Explorer.csproj --no-restore
```

Configuration and asset directories are copied beside the executable. They
must remain beside a published application at runtime.

## Publish

Create self-contained, single-file Windows x64 builds of both examples:

```powershell
./scripts/Publish-AllExamples.ps1
```

Published applications are written below `artifacts/publish`. The scripts also
support `linux-x64`, framework-dependent output, custom output directories, and
individual application publishing. See [scripts/README.md](scripts/README.md)
for the complete command reference and the intentional dependency-update
workflow.

The GitHub Actions workflow builds, tests, publishes, and uploads packaged
example applications for Windows x64 and Linux x64. Successful workflow runs
expose those packages as downloadable artifacts on the repository's Actions
page.

## Repository layout

| Path | Purpose |
| --- | --- |
| `Schreadt-Engine/` | Engine library |
| `Schreadt-Engine.Tests/` | Automated regression tests |
| `Example-Game/` | Platformer example application |
| `Mandelbrot-Explorer/` | Mandelbrot example application |
| `scripts/` | Reproducible publishing helpers |
| `.github/workflows/` | Windows and Linux continuous integration |

This README intentionally stays at the repository and build-workflow level;
engine usage documentation will be added separately.
