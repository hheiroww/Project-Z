# Your Project-Z application

Press **F5** to run the starter scene. Edit `MainScene` to add controls, text and graphics.

The **.NET desktop development** workload and **.NET 8 and .NET 10 SDKs** are required. The first build restores packages from NuGet.org.

Choose a renderer using the configuration selector in Visual Studio:

| Configuration | Renderer |
| --- | --- |
| Debug / Release | DirectX 12 |
| Debug-DX11 / Release-DX11 | DirectX 11 |
| Debug-OpenGL / Release-OpenGL | OpenGL |

You can also set **Project Properties > Project-Z Graphics > Graphics renderer**, or pass `-p:ProjectZGraphicsBackend=OpenGL` to `dotnet build`. Rebuild after changing renderer. Each named configuration has its own output and intermediate folders. When using command-line overrides, use a separate output folder or clean before switching.

DirectX 12 uses the public **ProjectZ 2026.10.25.2** NuGet runtime. The other configurations include matching framework assemblies compiled from the public Project-Z source, and restore the corresponding official MonoGame package. Keep the complete `ProjectZ` directory in source control with your application; it makes the renderers and build tools portable to other machines. The public package supplies the compiled free vector fonts, native scene XAML generator and standalone diagnostics observer in every configuration. This is a Windows x64 template; OpenGL changes the renderer, not the desktop operating system.

Build-time XAML is available through **Project Properties > Project-Z**. It starts disabled in this code-first template. To use the Visual Studio XAML designer with automatic shared view/code-behind conversion, create a **Project-Z XAML Desktop** solution instead.

Set **Project Properties > Project-Z > Rendering quality** to choose anti-aliasing and texture filtering, then rebuild. The defaults are **4x AA** and **16x anisotropic filtering**. These settings apply to native `Game`/`SceneManager` applications without enabling generated WPF startup or assigning quality in code. Explicit runtime overrides remain effective across scenes.

The project converter is ready to run from this directory:

```powershell
pwsh -File ./ProjectZ/Convert-Project.ps1 -SourceProject C:/Original/App.csproj -OutputDirectory C:/Converted/App
```

It preserves the original project and converts into a separate native tree with portable tools and renderer assemblies. Review its diagnostic reports; unsupported features block the generated build. No framework source checkout is required.

Framework and template source: https://github.com/hheiroww/Project-Z
