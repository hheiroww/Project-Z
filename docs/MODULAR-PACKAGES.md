# Smaller Project-Z applications

Starting with 2026.11.2.1, `ProjectZ` contains the scene graph, UI controls,
layout, input, text, rendering, runtime XAML loading, procedural 3D surfaces,
Windows integration and managed error logging. Optional dependencies no longer
flow into every application.

| Package | Install when the application needs |
| --- | --- |
| `ProjectZ` | The Windows native UI runtime. |
| `ProjectZ.Linux` / `ProjectZ.macOS` | The corresponding Linux or macOS DesktopGL runtime. Install one platform runtime per target. See [DesktopGL](DESKTOPGL.md). |
| `ProjectZ.ModelImport` | Assimp-backed OBJ/FBX loading through `ModelImporter3D` or `Surface3DElement.LoadModel`. |
| `ProjectZ.WebView2` | `ProjectZ.Shared.Html5.Html5EngineHost` and WebView2 HTML5 hosting. The separate Microsoft WebView2 browser runtime must also be available. |
| `ProjectZ.Diagnostics` | An external native crash observer, forced-exit detection and minidumps. Includes its own runtime and deploys as a sidecar executable. |
| `ProjectZ.XamlTools` | Build-time XAML conversion and native scene template compilation. Set `PrivateAssets="all"`; these tools stay on the developer/build machine. |
| `SocketJack` | Application networking. Reference SocketJack directly at the version required by the application. |

Use the same version for Project-Z co-packages:

```powershell
dotnet add package ProjectZ --version 2026.11.2.1
# Only for applications that load model files:
dotnet add package ProjectZ.ModelImport --version 2026.11.2.1
# Only for applications that need native crash dumps:
dotnet add package ProjectZ.Diagnostics --version 2026.11.2.1
```

For native scene XAML compilation:

```xml
<PackageReference Include="ProjectZ.XamlTools" Version="2026.11.2.1" PrivateAssets="all" />
```

## Migrating an application

Upgrade `ProjectZ`, then explicitly reference the optional features used by the
application. Publish into a fresh output directory when creating a new package
so binaries left by an older dependency graph do not enter the payload.
Existing model-loading APIs remain in the base assembly and load
the installed importer on demand. Calling them without the importer reports
which NuGet package to install. Creating procedural meshes needs no importer.
The base assembly no longer contains the HTML5 host; add `ProjectZ.WebView2`
to compile an application that uses it. WPF compatibility consumers retain their
own explicit WebView2 SDK dependency.

Managed exception reports, session logs and process-exit logging remain enabled
in the base runtime. Native fault dumps and forced-exit observation require
`ProjectZ.Diagnostics`. A base application does not start an observer, allocate
its shared context buffer or subscribe to first-chance exception snapshots.
The optional observer stays outside a single-file bundle so it can be started
reliably as a separate process. Distribute that sidecar when opting in.

Self-contained applications still contain the .NET/Windows Desktop runtime.
Framework-dependent deployment can share a separately installed runtime; it
requires that runtime on the recipient machine. Keep standalone installers
self-contained. WPF/WinForms trimming is not enabled by this change.

## Building and checking all packages

```powershell
./tools/Pack-RuntimePackages.ps1
./tests/Packaging/Verify.ps1 -PackagePath artifacts/runtime-packages/ProjectZ.2026.11.2.1.nupkg
```

The packaging test builds consumers outside the source tree. It checks the base
dependency graph and publish output, verifies managed logging without optional
features, imports a real OBJ through the optional importer, compiles HTML5 host
usage and native XAML, and verifies deployment of the optional observer.
Build output, temporary consumers and measurements are not source assets.

## Normal releases

Master pushes and default manual releases validate and publish all seven packages,
including `ProjectZ.Linux` and `ProjectZ.macOS`. GitHub releases contain the same
complete set. Platform packages use their native runner outputs; the macOS package
contains a universal capture helper. `validate_only=true` runs the complete gate
without publishing. [Release notes](RELEASE-NOTES.md) and [platform pathways](PLATFORM-PATHWAYS.md).
