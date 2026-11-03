# Project-Z templates for Visual Studio 2026

**[Download ProjectZ.VisualStudio2026.vsix](https://github.com/hheiroww/Project-Z/releases/latest/download/ProjectZ.VisualStudio2026.vsix)** · [Latest release and template checksums](https://github.com/hheiroww/Project-Z/releases/latest)

Version **2026.11.2.1** installs seven templates in Visual Studio 2026 on Windows:

| Template | Languages | Application platforms |
| --- | --- | --- |
| Project-Z Desktop | C#, Visual Basic | Windows x64; DX12, DX11 or OpenGL |
| Project-Z XAML Desktop | C#, Visual Basic | Windows x64; native app plus WPF Designer project |
| Project-Z Cross-Platform Desktop | C#, Visual Basic | Windows x64, Linux x64, Intel/Apple Silicon macOS |
| Project-Z Cross-Platform Scene XAML | C# | The same desktop platforms, with compiled native scene markup |

Install the **.NET desktop development** workload and **.NET 8 and .NET 10 SDKs**,
open the VSIX, select your Visual Studio 2026 instance and restart Visual Studio.
Choose **File > New > Project** and search **Project-Z**. Keep **All project types**
selected if another filter hides the templates. A per-user installation does not
require administrator access. Remove it through **Extensions > Manage Extensions**.

## Cross-platform starters

The three cross-platform templates target .NET 8 and select exactly one runtime
for the host OS or an explicit publish RID. Windows uses `ProjectZ` and DX12;
Linux/macOS use `ProjectZ.Linux`/`ProjectZ.macOS` and OpenGL. The native scene-XAML
variant compiles `MainScene.xaml` during ordinary builds through the build-only
`ProjectZ.XamlTools` package. It does not require WPF.

The release also contains a [.NET CLI template pack and ready-to-build ZIPs](../CrossPlatform/README.md),
usable without Visual Studio on every supported platform. Publish from any host:

```sh
dotnet publish MyApp.csproj -c Release -r linux-x64 --self-contained true
dotnet publish MyApp.csproj -c Release -r osx-x64 --self-contained true
dotnet publish MyApp.csproj -c Release -r osx-arm64 --self-contained true
dotnet publish MyApp.csproj -c Release -r win-x64 --self-contained true
```

For builds without a RID, `-p:ProjectZPlatform=Linux`, `macOS` or `Windows` selects
the target. See the [platform guide](../../docs/DESKTOPGL.md) for native desktop
dependencies, blur permissions and deployment limits. App bundles, signing and
notarization remain application deployment steps.

## Windows XAML designer and converter

Choose **Project-Z XAML Desktop** for an App project and a companion Designer
project. Set App as startup, open `Designer/MainWindow.xaml` in Design or Split
view, and edit its C# or VB event handlers in the Designer project. Save and build:
App translates the shared view and code-behind into native Project-Z sources under
`obj`. Unsupported markup or code reports diagnostics and fails the build. The
App does not reference the Designer assembly. See the [supported WPF surface](../../docs/WPF-IMPORT.md).

All four Windows templates include **Project Properties > Project-Z**, local
build tools and a portable WPF converter. Code-first Desktop templates leave
conversion disabled. Keep the complete `ProjectZ` directory and its notices in
your application's source control. No Project-Z checkout is required to build.
The framework is pinned to **2026.11.2.1**; native diagnostics, model import,
networking and browser integration are [optional packages](../../docs/MODULAR-PACKAGES.md).

From an App directory, convert an existing WPF project into a separate native tree:

```powershell
pwsh -File ./ProjectZ/Convert-Project.ps1 -SourceProject C:/Original/App.csproj -OutputDirectory C:/Converted/App
```

Conversion preserves its source and refuses to overwrite edited generated files.
Review `projectz-import.report.json` and `projectz-import.unsupported.txt`.
Unsupported features block the generated build. Converted output carries its
local tools and renderer assemblies so it can be relocated.

## Windows renderer and quality settings

| Configuration | Renderer |
| --- | --- |
| Debug / Release | DirectX 12, ProjectZ 2026.11.2.1 NuGet runtime |
| Debug-DX11 / Release-DX11 | DirectX 11, matching local framework assembly |
| Debug-OpenGL / Release-OpenGL | OpenGL, matching local Windows framework assembly |

Use the configuration selector or **Project Properties > Project-Z Graphics**.
Rebuild after switching. Named configurations separate their outputs; command-line
renderer overrides need a separate output directory. OpenGL in these Windows
templates retains Windows desktop integration. Choose a Cross-Platform template
for Linux/macOS applications.

**Project Properties > Project-Z > Rendering quality** applies the default 4x AA
and 16x anisotropic filtering without code changes. Existing Windows apps can
update `ProjectZFrameworkVersion` to **2026.11.2.1**; DX11/OpenGL apps also need the
matching `ProjectZ/Runtime/<renderer>/ProjectZ.dll` from this template. Desktop
blur uses the shared shader with native capture; see [setup](../../docs/DESKTOP-BLUR.md).

## Build and validate

Use Windows, the .NET 8/10 SDKs and Visual Studio/Build Tools with .NET Framework
4.7.2 targeting support. Keep the output path short for VSSDK ZIP extraction:

```powershell
./ProjectTemplate/VS2026/Build.ps1 -OutputDirectory C:/Build/ProjectZTemplates
./tests/ProjectTemplates/Verify.ps1 -BuildDirectory C:/Build/ProjectZTemplates
./tests/ProjectTemplates/Verify-Xaml.ps1 -BuildDirectory C:/Build/ProjectZTemplates
./tests/ProjectTemplates/Verify-CrossPlatform.ps1 -BuildDirectory C:/Build/ProjectZTemplates/Portable
```

The build keeps alternate renderers, published tooling, VSIX intermediates and
staging under the chosen output directory. `-SkipFrameworkBuild` reuses that
run's renderer assemblies. No cleanup runs automatically. `-PackagePath` on the
Windows build/verification scripts validates an exact local released package in
an isolated feed; omitting it tests public NuGet availability. Cross-platform
verification accepts `-PackageDirectory` for the four exact release packages.
Use `-SkipRuntime` on hosted Windows CI without a suitable GPU.

The template workflow checks both Windows languages/backends, XAML designers,
saved edits and relocated converters. It creates every CLI variant on Windows,
Linux, Intel macOS and Apple Silicon macOS, checks RID payloads, and verifies
rendered starter text on native Linux/macOS. After all jobs pass it attaches the
VSIX, CLI pack, three ZIPs and template checksums to the matching framework
release. Normal full framework releases call this workflow automatically.
