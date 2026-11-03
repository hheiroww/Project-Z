# Project-Z for Visual Studio 2026

One installer adds seven C# and Visual Basic project templates to **File > New > Project**.
Search for **Project-Z** after installing the extension and restarting Visual Studio.

| Template | Languages | Target platforms |
| --- | --- | --- |
| Project-Z Desktop | C#, Visual Basic | Windows x64 |
| Project-Z XAML Desktop | C#, Visual Basic | Windows x64, with a WPF designer project |
| Project-Z Cross-Platform Desktop | C#, Visual Basic | Windows x64, Linux x64, Intel and Apple Silicon macOS |
| Project-Z Cross-Platform Scene XAML | C# | Windows, Linux and macOS |

The Windows templates include DirectX 12, DirectX 11 and OpenGL configurations,
rendering-quality project properties, build-time XAML tooling and a WPF project
converter. The XAML Desktop templates pair a native application with a Visual
Studio designer project; saved view and code-behind edits convert during builds.

Cross-platform templates select the matching Project-Z runtime package for the
host or an explicit publish RID. Linux and macOS use OpenGL. The native scene-XAML
template compiles its scene markup during ordinary builds without a WPF dependency.
Generated applications build without a Project-Z source checkout.

Install the **.NET desktop development** workload and the **.NET 8 and .NET 10 SDKs**.
Visual Studio 2026 on Windows x64 is required to install these templates. Created
cross-platform projects can then be built with the .NET SDK on their target OS.
The first application build restores the pinned public runtime from NuGet.org.

Native desktop dependencies, macOS permissions, signing and notarization remain
application deployment requirements. Optional model importing, browser integration
and native crash diagnostics are separate runtime packages.

[Installation and usage](https://github.com/hheiroww/Project-Z/blob/master/ProjectTemplate/VS2026/README.md)
· [Platform guide](https://github.com/hheiroww/Project-Z/blob/master/docs/DESKTOPGL.md)
· [Source and issues](https://github.com/hheiroww/Project-Z)

The matching physics runtime adds opt-in rigid bodies, parent-bound collisions, FreeMove dragging, weighted scrolling, friction presets, and 3D sprite/GIF particle emitters. Physics drives existing animation parameters; collision solving runs on the CPU and particle rendering uses GPU batching. See the [physics guide](https://github.com/hheiroww/Project-Z/blob/master/docs/PHYSICS.md).
