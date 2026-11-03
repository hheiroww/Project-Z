# Cross-platform templates in the combined VSIX

Install **ProjectZ.VisualStudio2026.vsix** from the
[latest Project-Z release](https://github.com/hheiroww/Project-Z/releases/latest).
It installs all seven templates in Visual Studio 2026, including:

- **Project-Z Cross-Platform Desktop (C#)**
- **Project-Z Cross-Platform Desktop (Visual Basic)**
- **Project-Z Cross-Platform Scene XAML (C#)**

See the [VSIX installation and template guide](../VS2026/README.md).
Create a project in Visual Studio, then keep its source in your own repository.
The generated cross-platform project can build on Windows, Linux and macOS with
.NET 8 or later. A publish RID selects the corresponding Project-Z runtime:

```sh
dotnet publish MyApp.csproj -c Release -r linux-x64 --self-contained true
dotnet publish MyApp.csproj -c Release -r osx-x64 --self-contained true
dotnet publish MyApp.csproj -c Release -r osx-arm64 --self-contained true
dotnet publish MyApp.csproj -c Release -r win-x64 --self-contained true
```

Linux/macOS use OpenGL; Windows uses DX12. Native scene XAML compiles during
ordinary builds through the build-only `ProjectZ.XamlTools` package. The native
scene-XAML starter does not need WPF. The separate WPF designer templates in the
same installer retain their Windows target.

See [platform dependencies and deployment](../../docs/DESKTOPGL.md), including
macOS desktop permissions, app bundles, signing and notarization. The template
build creates internal CLI/ZIP fixtures to validate the same portable sources on
native CI hosts; the public template download is the combined VSIX.