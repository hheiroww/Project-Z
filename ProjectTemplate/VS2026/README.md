# Project-Z templates for Visual Studio 2026

**[Download ProjectZ.VisualStudio2026.vsix](https://github.com/hheiroww/Project-Z/releases/download/vs2026-2026.10.20.1/ProjectZ.VisualStudio2026.vsix)** · [Release and SHA-256 checksums](https://github.com/hheiroww/Project-Z/releases/tag/vs2026-2026.10.20.1)

The installer adds **Project-Z Desktop (C#)** and **Project-Z Desktop (Visual Basic)** to Visual Studio 2026, including 2026 Insiders, on Windows x64. Each creates a .NET 10 Windows application with a running Project-Z starter scene, compiled fonts, and standalone crash diagnostics. Install the **.NET desktop development** workload and **.NET 10 SDK** first. Administrator access is not required for a per-user VSIX install.

Open the `.vsix`, choose the Visual Studio 2026 instance, and complete installation. Restart Visual Studio, then choose **File → New → Project** and search **Project-Z**. Select either language. Keep the default **All project types** filter if an existing filter hides the templates. Press **F5** to run. Remove the templates through **Extensions → Manage Extensions**.

## Select a renderer

| Visual Studio configuration | Framework and MonoGame renderer |
| --- | --- |
| Debug / Release | DirectX 12, the public ProjectZ 2026.10.20.1 runtime |
| Debug-DX11 / Release-DX11 | DirectX 11, MonoGame WindowsDX 3.8.5.1 |
| Debug-OpenGL / Release-OpenGL | OpenGL, MonoGame DesktopGL 3.8.5.1 |

Use the configuration selector or **Project Properties → Project-Z Graphics → Graphics renderer**. Rebuild after changing the property. The named configurations keep their output and intermediate directories separate. The starter scene displays the renderer actually compiled into the framework.

```powershell
dotnet build MyApp.csproj -c Debug-OpenGL
dotnet build MyApp.csproj -c Release-DX11
dotnet build MyApp.csproj -c Release
```

`-p:ProjectZGraphicsBackend=OpenGL` also works; use a separate output directory or clean before switching a command-line override. Supported values are `DirectX12`, `DirectX11` and `OpenGL`. Unsupported values fail the build. This template targets **Windows x64**; OpenGL selects the graphics API and does not make the Windows desktop integration cross-platform.

The public ProjectZ package is pinned to **2026.10.20.1** and supplies common dependencies, content, the native XAML generator and diagnostics. DX11/OpenGL select the corresponding `ProjectZ/Runtime/<renderer>/ProjectZ.dll` shipped in the template and exclude the package's DX12 managed graphics assets. These backend assemblies are built from this repository with the matching shaders. Keep that directory, its notices and licenses in your application's source control. A consuming machine only needs Visual Studio/.NET and access to NuGet.org; it does not need this source repository.

## Build the installer from source

Use Windows, the .NET 8 and .NET 10 SDKs, and Visual Studio or Build Tools 2022 or later with .NET Framework 4.7.2 targeting support. The VSSDK build tools restore from NuGet; no running extension or wizard code is installed. The resulting VSIX targets Visual Studio 2026.

```powershell
./ProjectTemplate/VS2026/Build.ps1
./tests/ProjectTemplates/Verify.ps1
# After installing the VSIX on Visual Studio 2026:
./tests/ProjectTemplates/Verify-VisualStudio.ps1
```

Output: `artifacts/VS2026/ProjectZ.VisualStudio2026.vsix`. The build compiles both alternate framework backends, generates explicit template manifests, and packages them with Microsoft's VSIX tools, including template discovery metadata. Only the template source allowlist and the two backend DLLs enter the installer. PDBs, build logs and local settings are excluded.

Verification restores into a separate package directory from NuGet.org, creates both standalone projects, builds each renderer, runs the native windows, checks the actual backend and rendered pixels, saves PNG captures, and checks rejection of an invalid backend. Use `-SkipRuntime` on a build server without an interactive graphics session.

Framework source builds can also select OpenGL directly:

```powershell
dotnet build 'Project Z Windows.vbproj' -c Release -p:ProjectZGraphicsBackend=OpenGL -p:GeneratePackageOnBuild=false
```
