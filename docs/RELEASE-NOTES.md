# Project-Z 2026.11.4.1

Desktop shader blur now requests capture immediately when the host moves,
resizes or changes DPI, independently of the stationary refresh limit. The
default stationary cadence is 60 FPS. Bounded overscan and reusable capture
buffers keep the desktop anchored during reads and reduce resize allocation.
Completed frames are uploaded immediately before drawing as well as during ticks.

The new per-host `DesktopBackdrop.RenderRevision` tells applications when their
idle drawing and native alpha-surface presentation gates need to refresh.
`SceneManager.Tick` also invalidates the active scene for backdrop changes.
Windows capture reuses staging capacity during resizing and optimizes the common
single-display pixel conversion path while preserving host exclusion.
Windows native move/resize loops continue the game presentation path, and the
imported-window host observes backdrop revisions in its idle drawing gate.

The GitHub and NuGet readmes now share current UTF-8 text, correcting corrupt
separators and arrows and updating the embedded package documentation.

All seven runtime packages are released together: ProjectZ (Windows),
ProjectZ.Linux, ProjectZ.macOS, ProjectZ.ModelImport, ProjectZ.WebView2,
ProjectZ.Diagnostics and ProjectZ.XamlTools. Physics remains opt-in; existing
controls keep their normal behavior until configured.

## One installable template extension

Download **ProjectZ.VisualStudio2026.vsix** to install all seven templates in
Visual Studio 2026. It replaces the earlier split template downloads and retains
the same extension ID for upgrades. Every template uses the matching 2026.11.4.1
runtime; the bundled Windows DX11/OpenGL assemblies and XAML tooling are rebuilt
from this release.

The installer includes Windows Desktop and XAML Designer templates in C# and
Visual Basic, Cross-Platform Desktop templates in both languages, and a C# native
scene-XAML template. Created cross-platform projects support Windows x64, Linux
x64, Intel macOS and Apple Silicon macOS. No framework checkout is required.

[Template installation](https://github.com/hheiroww/Project-Z/blob/master/ProjectTemplate/VS2026/README.md)
· [Physics APIs and limits](https://github.com/hheiroww/Project-Z/blob/master/docs/PHYSICS.md)
· [Desktop blur and permissions](https://github.com/hheiroww/Project-Z/blob/master/docs/DESKTOP-BLUR.md)
· [Platform integration](https://github.com/hheiroww/Project-Z/blob/master/docs/PLATFORM-PATHWAYS.md)

[Install the combined VSIX from Visual Studio Marketplace](https://marketplace.visualstudio.com/items?itemName=SocketJack.ProjectZVisualStudio2026). Every project template has its own bundled icon; the package targets stable 17.14 APIs supported by Visual Studio 2026.
