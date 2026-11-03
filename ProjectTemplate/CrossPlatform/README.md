# Cross-platform Project-Z templates

Download **ProjectZ.Templates.2026.11.2.1.nupkg** from the
[latest framework release](https://github.com/hheiroww/Project-Z/releases/latest).
Install it with a .NET 8 or later SDK on Windows, Linux or macOS:

```sh
dotnet new install ./ProjectZ.Templates.2026.11.2.1.nupkg
dotnet new projectz-desktop -n MyApp -lang C#
dotnet new projectz-desktop -n MyVbApp -lang VB
dotnet new projectz-scene -n MyXamlApp
dotnet run --project MyApp
```

The C# and VB desktop starters create their scene in code. The C# scene-XAML
starter compiles native `MainScene.xaml` on each build using the optional
`ProjectZ.XamlTools` build dependency. All three target .NET 8 and select exactly
one runtime: `ProjectZ` for Windows, `ProjectZ.Linux` for Linux, or `ProjectZ.macOS`
for macOS. A publish RID selects its target even when building on another OS:

```sh
dotnet publish MyApp -c Release -r linux-x64 --self-contained true
dotnet publish MyApp -c Release -r osx-x64 --self-contained true
dotnet publish MyApp -c Release -r osx-arm64 --self-contained true
dotnet publish MyApp -c Release -r win-x64 --self-contained true
```

The same release includes three ready-to-build `ProjectZ.CrossPlatform.*.zip`
starters and SHA-256 checksums. Extract a ZIP and run `dotnet run` in its folder;
rename the project/namespace if desired. Visual Studio 2026 users can install
the [VSIX](../VS2026/README.md), which includes these three templates alongside
the four Windows desktop and WPF-designer templates.

Linux/macOS use OpenGL; Windows uses DX12. The generated README describes native
dependencies, platform overrides and desktop blur permissions. Read the
[platform guide](../../docs/DESKTOPGL.md) before deployment. macOS signing,
bundles and notarization remain application deployment steps. The WPF designer
templates retain a Windows target; the native scene-XAML starter runs on all
three desktop platforms.

```powershell
./ProjectTemplate/CrossPlatform/Build.ps1
./tests/ProjectTemplates/Verify-CrossPlatform.ps1 -SkipRuntime
```

The template pack contains only explicit starter sources and configuration.
No framework binaries, model libraries, source checkout paths, package feeds or
credentials are bundled. Verification installs into an isolated .NET template
hive, creates each language/variant, validates platform selection, publishes
supported RIDs, and checks native assets and package graphs. Native CI runs the
generated applications and verifies scene rendering on Linux and macOS.
