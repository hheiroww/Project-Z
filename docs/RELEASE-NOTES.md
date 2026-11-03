# Project-Z 2026.11.2.2

This release adds opt-in physics that drives the existing scene and animation
parameters, weighted scrolling, collision-enabled parent bounds, stable overlap
recovery, bounce, friction presets and FreeMove pointer dragging. 3D surfaces and
bounded sprite/GIF particle emitters use the same scene renderer. Collision
simulation runs on the CPU; particle rendering uses GPU batching.

All seven runtime packages are released together: ProjectZ (Windows),
ProjectZ.Linux, ProjectZ.macOS, ProjectZ.ModelImport, ProjectZ.WebView2,
ProjectZ.Diagnostics and ProjectZ.XamlTools. Physics remains opt-in; existing
controls keep their normal behavior until configured.

## One installable template extension

Download **ProjectZ.VisualStudio2026.vsix** to install all seven templates in
Visual Studio 2026. It replaces the earlier split template downloads and retains
the same extension ID for upgrades. Every template uses the matching 2026.11.2.2
runtime; the bundled Windows DX11/OpenGL assemblies and XAML tooling are rebuilt
from this physics release.

The installer includes Windows Desktop and XAML Designer templates in C# and
Visual Basic, Cross-Platform Desktop templates in both languages, and a C# native
scene-XAML template. Created cross-platform projects support Windows x64, Linux
x64, Intel macOS and Apple Silicon macOS. No framework checkout is required.

[Template installation](https://github.com/hheiroww/Project-Z/blob/master/ProjectTemplate/VS2026/README.md)
· [Physics APIs and limits](https://github.com/hheiroww/Project-Z/blob/master/docs/PHYSICS.md)
· [Desktop blur and permissions](https://github.com/hheiroww/Project-Z/blob/master/docs/DESKTOP-BLUR.md)
· [Platform integration](https://github.com/hheiroww/Project-Z/blob/master/docs/PLATFORM-PATHWAYS.md)

[Install the combined VSIX from Visual Studio Marketplace](https://marketplace.visualstudio.com/items?itemName=SocketJack.ProjectZVisualStudio2026). Every project template has its own bundled icon; the package targets stable 17.14 APIs supported by Visual Studio 2026.
