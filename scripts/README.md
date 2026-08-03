# Publishing the examples

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
