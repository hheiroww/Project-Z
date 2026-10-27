# Project-Z templates for Visual Studio 2026

**[Download ProjectZ.VisualStudio2026.vsix](https://github.com/hheiroww/Project-Z/releases/download/vs2026-2026.10.25.2/ProjectZ.VisualStudio2026.vsix)** · [Release and SHA-256 checksums](https://github.com/hheiroww/Project-Z/releases/tag/vs2026-2026.10.25.2)

The installer adds **Project-Z Desktop** and **Project-Z XAML Desktop**, each in **C#** and **Visual Basic**, to Visual Studio 2026, including 2026 Insiders, on Windows x64. Each creates a .NET 10 Windows application with compiled free vector fonts and standalone crash diagnostics. Install the **.NET desktop development** workload and **.NET 8 and .NET 10 SDKs** first. Administrator access is not required for a per-user VSIX install.

Open the `.vsix`, choose the Visual Studio 2026 instance, and complete installation. Restart Visual Studio, then choose **File → New → Project** and search **Project-Z**. Select either language. Keep the default **All project types** filter if an existing filter hides the templates. Press **F5** to run. Remove the templates through **Extensions → Manage Extensions**.

## XAML designer and automatic builds

Choose **Project-Z XAML Desktop** to create an **App** project and a companion **Designer** project. Set App as the startup project. Open `Designer/MainWindow.xaml` in **Design** or **Split** view and edit the view with Visual Studio's WPF designer. Edit its event handlers in `MainWindow.xaml.cs` or `MainWindow.xaml.vb`.

App links those files and enables `ProjectZXamlMode=DesignerAndCodeBehind`. Saving and building regenerates native Project-Z designers and translates the shared code-behind under `obj`; the native app does not reference the WPF Designer assembly. Subsequent builds include saved XAML edits automatically. Unsupported markup or code produces diagnostics and fails the build. The supported WPF surface is described in [the importer guide](../../docs/WPF-IMPORT.md).

Release **2026.10.25.1** keeps the linked WPF code-behind out of App's `Compile` items and feeds it through `ProjectZXamlSource`. IntelliSense and ordinary builds see the generated native window class. To repair an older generated app, change its linked code-behind item from `Compile` to `None`, copy the updated `ProjectZ/ProjectZ.Xaml.targets` from this template, and rebuild. The framework no longer adds the unused HTML5 engine files to new projects; imported WebView2 controls remain supported.

All four templates include **Project Properties → Project-Z** for build-time settings and the complete portable tooling in `ProjectZ`. Code-first desktop projects leave conversion disabled until you select a mode and add `ProjectZXaml` inputs. Keep generated files under `obj` and edit the original inputs. No Project-Z source checkout is required on the consuming machine.

Release **2026.10.25.2** applies **Project Properties → Project-Z → Rendering quality** to each native `Game`/`SceneManager`, including desktop and XAML apps. The default **4x AA** and **16x anisotropic filtering** work without assignments in `GameWindow` and without enabling generated WPF startup. Rebuild after changing these properties. Settings initialize once per graphics device, so later explicit code overrides remain effective across scenes.

For an existing DX12 template app, change `ProjectZFrameworkVersion` in `ProjectZ/ProjectZ.Graphics.props` to **2026.10.25.2** and rebuild. The package supplies the rendering settings build target, so older copied application targets also receive the fix. For DX11/OpenGL, also replace the corresponding `ProjectZ/Runtime/<renderer>/ProjectZ.dll` with the matching version from this template. Remove any manual AA workaround when you want the project property to control quality.

## Project converter utility

Every created App project includes `ProjectZ/Convert-Project.ps1`. Convert an existing C# or VB WPF project and its project references into a separate native project tree:

```powershell
pwsh -File ./ProjectZ/Convert-Project.ps1 -SourceProject C:/Original/App.csproj -OutputDirectory C:/Converted/App
```

Run from the app directory. `-RootDirectory App` optionally selects a stable generated entry folder. The utility prints the generated project to open in Visual Studio. It preserves source files, tracks its generated output, and refuses to overwrite edited generated files. Unsupported features appear in `projectz-import.report.json` and `projectz-import.unsupported.txt` and block the generated build. Converted output carries its tools, compatibility assembly, renderer assemblies and notices, so the generated tree can be moved to another machine. Rerun conversion after editing the original application.

## Select a renderer

| Visual Studio configuration | Framework and MonoGame renderer |
| --- | --- |
| Debug / Release | DirectX 12, the public ProjectZ 2026.10.25.2 runtime |
| Debug-DX11 / Release-DX11 | DirectX 11, MonoGame WindowsDX 3.8.5.1 |
| Debug-OpenGL / Release-OpenGL | OpenGL, MonoGame DesktopGL 3.8.5.1 |

Use the configuration selector or **Project Properties → Project-Z Graphics → Graphics renderer**. Rebuild after changing the property. The named configurations keep their output and intermediate directories separate. The starter scene displays the renderer actually compiled into the framework.

```powershell
dotnet build MyApp.csproj -c Debug-OpenGL
dotnet build MyApp.csproj -c Release-DX11
dotnet build MyApp.csproj -c Release
```

`-p:ProjectZGraphicsBackend=OpenGL` also works; use a separate output directory or clean before switching a command-line override. Supported values are `DirectX12`, `DirectX11` and `OpenGL`. Unsupported values fail the build. This template targets **Windows x64**; OpenGL selects the graphics API and does not make the Windows desktop integration cross-platform.

The public ProjectZ package is pinned to **2026.10.25.2** and supplies common dependencies, content, the native scene XAML generator and diagnostics. The template also supplies the full WPF view/project converter and compatibility assembly. DX11/OpenGL select the corresponding `ProjectZ/Runtime/<renderer>/ProjectZ.dll` shipped in the template and exclude the package's DX12 managed graphics assets. These backend assemblies are built from this repository with the matching shaders. Keep the complete `ProjectZ` directory, its notices and licenses in your application's source control.

## Build the installer from source

Use Windows, the .NET 8 and .NET 10 SDKs, and Visual Studio or Build Tools 2022 or later with .NET Framework 4.7.2 targeting support. The VSSDK build tools restore from NuGet; no running extension or wizard code is installed. The resulting VSIX targets Visual Studio 2026.

```powershell
./ProjectTemplate/VS2026/Build.ps1
./tests/ProjectTemplates/Verify.ps1
./tests/ProjectTemplates/Verify-Xaml.ps1
# After installing the VSIX on Visual Studio 2026:
./tests/ProjectTemplates/Verify-VisualStudio.ps1
```

Output: `artifacts/VS2026/ProjectZ.VisualStudio2026.vsix` and `SHA256SUMS.txt`. The build compiles both alternate framework backends, publishes the XAML converter, builds the compatibility assembly against the pinned public package, generates explicit single-project and solution template manifests, and packages them with Microsoft's VSIX tools. Only template sources, published tool binaries/data, renderer/compatibility assemblies and notices enter the installer. PDBs, build logs and local settings are excluded.

Verification restores standalone projects from NuGet.org, builds both languages with each renderer, runs the native windows, checks the actual backend and rendered pixels, and saves PNG captures. XAML checks also compile the designer projects, verify edited markup and native button events, reject unsupported markup, and build converted projects after relocation. IDE verification uses Visual Studio's actual template creation API. Use `-SkipRuntime` on a build server without an interactive graphics session.

For a release candidate that NuGet.org has not indexed yet, pass the exact framework package to the build and verification scripts:

```powershell
dotnet build 'Project Z Windows.vbproj' -c Release -p:UseLocalSocketJack=false -p:ProjectZGraphicsBackend=DirectX12
$package = Resolve-Path 'bin/Release/ProjectZ.2026.10.25.2.nupkg'
./ProjectTemplate/VS2026/Build.ps1 -PackagePath $package
./tests/ProjectTemplates/Verify.ps1 -PackagePath $package
./tests/ProjectTemplates/Verify-Xaml.ps1 -PackagePath $package
```

The scripts validate the package ID and pinned version, map `ProjectZ` exclusively to an isolated local feed, and verify the restored package hash. Other dependencies restore from NuGet.org. Each verification uses a fresh cache, including converted and relocated projects. Omitting `-PackagePath` retains public-only acceptance; a prior local-package run cannot satisfy that check from its cache.

The template workflow builds and audits this exact package from its checkout before creating the installer. Its success proves local-package template acceptance independently of NuGet publishing/indexing. Run verification without `-PackagePath` separately to establish public package availability. Generated templates keep their normal public package references; feeds, caches and verification receipts stay outside the installer.

Framework source builds can also select OpenGL directly:

```powershell
dotnet build 'Project Z Windows.vbproj' -c Release -p:ProjectZGraphicsBackend=OpenGL -p:GeneratePackageOnBuild=false
```
