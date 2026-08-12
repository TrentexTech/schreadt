# Repository guidance for coding agents

These instructions apply to the complete repository.

## Working approach

- Inspect the live tree and relevant tests before making repository-specific
  claims or edits.
- Keep changes focused on the requested behavior. Preserve unrelated and
  pre-existing working-tree changes.
- Do not commit, push, publish, or edit files under `Documentation/` unless the
  user explicitly requests that action.
- Do not edit generated output under `bin/`, `obj/`, `artifacts/`, or
  `TestResults/`.
- Diagnose runtime failures from a fresh executable log under
  `bin/<Configuration>/net10.0/logs/`. Search for `[FATAL]` and `[ERROR]` and
  add a regression test for the failing transition when practical.

## SDK and dependencies

- Run commands from the repository root.
- Use the exact SDK pinned by `global.json`.
- NuGet locked restore is enabled repository-wide. Do not edit
  `packages.lock.json` manually.
- When intentionally changing dependencies, update the project reference,
  regenerate locks with `dotnet restore Schreadt.slnx --force-evaluate`, review
  the lock-file diff, and confirm a normal locked restore succeeds afterward.

## Validation

Use these canonical commands:

```powershell
dotnet restore Schreadt.slnx
dotnet build Schreadt.slnx --no-restore
dotnet test --solution Schreadt.slnx --no-restore
```

- Use the `--solution` form for solution-wide tests; positional
  `dotnet test Schreadt.slnx` is not valid with this repository's test runner.
- Validate both Debug and Release for changes involving build configuration,
  publishing, unsafe/native interop, resource lifetime, or platform behavior.
- Prefer focused tests while iterating, then run the complete suite before
  handing off a completed engine change.
- Compilation alone is insufficient for runtime, rendering, input, or native
  lifecycle fixes. Perform a proportionate live SDL/OpenGL check when the local
  environment supports it.
- Run `git diff --check` before handoff.

## Project conventions

- Target framework and nullable settings belong in the existing project files;
  shared restore and publish settings belong in `Directory.Build.props`.
- Keep engine behavior in `Schreadt-Engine`, example-specific behavior in its
  example project, and regression coverage in `Schreadt-Engine.Tests`.
- Preserve ownership, lifecycle ordering, and idempotent cleanup invariants.
  Do not bypass engine-owned orchestration from example code.
- Keep native SDL and OpenGL operations on their owning thread unless a tested
  synchronization and context-transfer design explicitly permits otherwise.
- Treat `config/` and `assets/` as required external publish content; they are
  deliberately excluded from the single executable and copied beside it.
- Use the publishing helpers in `scripts/` instead of duplicating publish
  command lines.

## Documentation boundary

Root-level onboarding may describe repository purpose, prerequisites, commands,
examples, and layout. Do not introduce detailed engine API documentation,
tutorials, or architectural claims unless that documentation is explicitly in
scope and has been verified against the current implementation.
