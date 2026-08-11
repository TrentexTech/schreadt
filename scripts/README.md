# Building and publishing

## Reproducible SDK and restore

Schreadt targets .NET 10 and pins SDK 10.0.100 exactly through the root
`global.json`. SDK roll-forward and prerelease selection are disabled because
SDK servicing releases can change implicit build dependencies such as
`Microsoft.NET.ILLink.Tasks`. Confirm the selected SDK from the repository root:

```powershell
dotnet --version
```

Every project has a committed `packages.lock.json`, and the shared build settings
enable locked mode by default. A normal restore therefore fails instead of
silently choosing different direct or transitive package versions:

```powershell
dotnet restore Schreadt.slnx
dotnet build Schreadt.slnx --no-restore
dotnet test --solution Schreadt.slnx --no-restore
```

To update a dependency, change its `PackageReference`, regenerate all affected
locks, and review the lock-file diff before committing:

```powershell
dotnet restore Schreadt.slnx --force-evaluate
dotnet restore Schreadt.slnx
```

Do not edit `packages.lock.json` manually.

The lock graph includes the supported `win-x64` and `linux-x64` single-file
publish targets. The publishing scripts also enable locked restore when a
runtime-specific restore is required, so publishing cannot silently change the
dependency graph.

## Publishing the examples

Run these commands from the repository root in PowerShell. The scripts themselves resolve
all project paths relative to the repository, so invoking them by absolute path also works
from another directory:

```powershell
.\scripts\Publish-ExampleGame.ps1
.\scripts\Publish-MandelbrotExplorer.ps1
.\scripts\Publish-AllExamples.ps1
```

By default, the scripts create self-contained Windows x64 single-file applications under
`artifacts\publish\<application>\win-x64`. Keep each generated `config` and `assets`
directory beside its executable.

Common options:

```powershell
# Target another runtime.
.\scripts\Publish-ExampleGame.ps1 -Runtime linux-x64

# Create a smaller executable that requires an installed .NET runtime.
.\scripts\Publish-ExampleGame.ps1 -FrameworkDependent

# Select a custom output directory relative to the repository root.
.\scripts\Publish-ExampleGame.ps1 -OutputDirectory publish\platformer

# Skip restore when the selected runtime was already restored.
.\scripts\Publish-ExampleGame.ps1 -NoRestore
```

The scripts intentionally do not enable assembly trimming because asset library types are
resolved through reflection.
