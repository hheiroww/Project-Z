# Project-Z 2026.11.2.1

Linux and macOS join the normal Project-Z release set. Every version builds,
validates and distributes these seven packages together:

- `ProjectZ` (Windows)
- `ProjectZ.Linux`
- `ProjectZ.macOS`
- `ProjectZ.ModelImport`
- `ProjectZ.WebView2`
- `ProjectZ.Diagnostics`
- `ProjectZ.XamlTools`

The desktop material now uses a shared GPU Gaussian shader on DX12, DX11 and
OpenGL. Native capture omits the host to prevent feedback; Windows capture
exclusion is restored on shutdown. macOS requires Screen Recording permission.
Linux uses XComposite/XRender on X11 and offers managed dependency installation
with an opaque fallback. Installing X11 components does not change an active
Wayland session; a compatible session or compositor provider is still required.

Applications can provide font/shaping, spelling, video playback and model-import
backends through supported hooks, keeping optional native engines out of the
base UI package. Existing Windows defaults remain available for text, spelling
and media. DWM material requests now use the shader and no companion window.

[Desktop blur and setup](https://github.com/hheiroww/Project-Z/blob/master/docs/DESKTOP-BLUR.md) · [Platform integration pathways](https://github.com/hheiroww/Project-Z/blob/master/docs/PLATFORM-PATHWAYS.md)
