# Project-Z 2026.11.20.1

- Desktop blur processes pending input and animation updates before capture polling. The renderer uploads the newest ready frame when drawing and skips redundant filtering and target switches.
- The Gaussian and material shader passes retain the existing blur kernel, saturation, tint and noise. Cached desktop pixels stay aligned while windows move and resize.
- The runtime includes the expanded shader preset library, editable surface materials, water simulation, animation rig imports, two-bone IK and configurable scrollbar thickness.
- `ProjectZ` targets Windows x64 and ARM64 with DirectX 12 and matching native capture and shader compiler assets.
- `ProjectZ.Linux` targets Linux x64 and ARM64. `ProjectZ.macOS` supports Intel and Apple Silicon using the same universal package.
- The cross-platform desktop and native scene XAML starters accept all six desktop RIDs.

The runtime packages and combined Visual Studio template extension use the same framework version.
