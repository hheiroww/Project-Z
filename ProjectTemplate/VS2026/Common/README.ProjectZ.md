# Your Project-Z application

Press **F5** to run the starter scene. Edit `MainScene` to add controls, text and graphics.

The **.NET desktop development** workload and **.NET 10 SDK** are required. The first build restores packages from NuGet.org.

Choose a renderer using the configuration selector in Visual Studio:

| Configuration | Renderer |
| --- | --- |
| Debug / Release | DirectX 12 |
| Debug-DX11 / Release-DX11 | DirectX 11 |
| Debug-OpenGL / Release-OpenGL | OpenGL |

You can also set **Project Properties > Project-Z Graphics > Graphics renderer**, or pass `-p:ProjectZGraphicsBackend=OpenGL` to `dotnet build`. Rebuild after changing renderer. Each named configuration has its own output and intermediate folders. When using command-line overrides, use a separate output folder or clean before switching.

DirectX 12 uses the public **ProjectZ 2026.10.20.1** NuGet runtime. The other configurations include matching framework assemblies compiled from the public Project-Z source, and restore the corresponding official MonoGame package. Keep the `ProjectZ/Runtime` directory in source control with your application; it makes those configurations portable to other machines. The public package supplies the compiled fonts, native XAML generator and standalone diagnostics observer in every configuration. This is a Windows x64 template; OpenGL changes the renderer, not the desktop operating system.

Framework and template source: https://github.com/hheiroww/Project-Z
