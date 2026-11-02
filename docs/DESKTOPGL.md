# Project-Z on Linux and macOS

`ProjectZ.Linux` and `ProjectZ.macOS` use MonoGame DesktopGL 3.8.5.1 (SDL2 and
OpenGL) and the same maintained VB.NET scene, control, layout, XAML, animation,
effects and procedural 3D sources as the Windows runtime. Both packages target
`net8.0`; .NET 8 and later desktop applications can use them. No Windows desktop
targeting pack, WPF, WinForms, .NET macOS workload or Wine is needed for the managed
runtime. Building the macOS capture helper from source requires Xcode command-line
tools; NuGet consumers receive that universal helper.

Install **one** platform runtime in an application:

```sh
dotnet add package ProjectZ.Linux --version 2026.11.2.1
# Or, for a Mac:
dotnet add package ProjectZ.macOS --version 2026.11.2.1
```

The platform packages each supply `ProjectZ.dll`. Do not reference both, or
combine either with the Windows `ProjectZ` package, in the same target framework.
For a multi-platform repository, condition the package reference on your target
platform. Keep a separate Windows target when using Windows integrations.

| Feature | Linux / macOS |
| --- | --- |
| Scenes, UI controls, layouts, runtime scene XAML, animations, masks, GPU effects | Shared runtime |
| Keyboard, Unicode text events, mouse, resizing and text clipboard | MonoGame / SDL2 |
| Vector text, regular / semibold / bold | Compiled ProjectZ Sans outlines, with its SIL OFL notice included |
| Requested system font families | Fall back to ProjectZ Sans; unsupported characters use its missing-glyph outline |
| Custom font files, shaping and color glyphs | Register `VectorFont.Provider`; the built-in DesktopGL fallback remains ProjectZ Sans |
| Spelling | Set `OfflineSpellChecker.Provider`; `IsAvailable` reports the configured engine. Built-in Windows spelling remains Windows only. |
| Images and application-supplied video frames | Supported, including `MediaElement.PresentFrame` |
| Video playback | Attach `MediaElement.PlaybackController` and deliver frames through `PresentFrame`; the built-in decoder remains Windows only |
| Desktop blur | Shared shader with native capture and exclusion; [setup and session limits](DESKTOP-BLUR.md) |
| Native chrome, hooks, WebView2 and crash observer | Use SDL/framework controls, a browser adapter and portable diagnostics; [all pathways](PLATFORM-PATHWAYS.md) |

The SDL/OpenAL native libraries flow from MonoGame's NuGet dependencies. Publish
for an explicit RID so the SDK selects the appropriate libraries. Linux requires
a graphical session and a working OpenGL driver; macOS requires an OpenGL-capable
desktop session. Application signing, app bundles and notarization are separate
deployment steps.

```sh
dotnet publish MyApp.csproj -c Release -r linux-x64 --self-contained true
dotnet publish MyApp.csproj -c Release -r osx-x64 --self-contained true
dotnet publish MyApp.csproj -c Release -r osx-arm64 --self-contained true
```

## Build and verify from source

```sh
dotnet build "Project Z Linux/ProjectZ.Linux.vbproj" -c Release
dotnet build "Project Z macOS/ProjectZ.macOS.vbproj" -c Release
dotnet build "Project Z Linux Test/Project Z Linux Test.vbproj" -c Release
```

The Linux sample accepts `-p:ProjectZDesktopPlatform=macOS` to build against the
Mac target. It uses the new VB runtime; the retired KNI/C# Linux project is no
longer a build entry point. `Project Z Desktop.slnx` groups the portable projects
for .NET 9+ SDKs and recent Visual Studio versions; individual projects also build
with the .NET 8 SDK.

```powershell
./tools/Pack-DesktopGLPackages.ps1
./tests/DesktopGL/Verify.ps1 -RuntimeIdentifier linux-x64
# Requires a desktop session (or xvfb on Linux):
./tests/DesktopGL/Verify.ps1 -RuntimeIdentifier linux-x64 -RunGraphics
```

Verification uses an independent NuGet consumer outside the source tree, checks
the dependency graph, loads fonts, validates both embedded OpenGL shaders, and
publishes Linux x64 and Intel / Apple Silicon macOS payloads. Native CI runs the
build and checks on Ubuntu and macOS as well as Windows.

The main `dotnet.yml` workflow validates Windows and all DesktopGL matrix jobs,
then assembles one seven-package version for GitHub and NuGet. Normal master pushes
and default manual releases include both platform packages. The release takes
Linux from Ubuntu and the universal macOS package from the Apple Silicon Mac job.
`validate_only=true` verifies the complete set without publishing;
`desktopgl_only=true` with `publish_desktopgl=true` remains available for a scoped release. Windows hosted CI checks
builds and publishing without graphics; native Linux and Mac jobs also run the
graphics, Unicode input and clipboard smoke tests.

OpenGL shader binaries are intentionally versioned source assets so Linux and
Mac builds do not need the Windows HLSL compiler. After editing either shader or
the MonoGame tool manifest, regenerate them on Windows with
`./tools/Build-DesktopGLShaders.ps1` and commit the binaries and fingerprint file
together. Builds reject stale shaders.
