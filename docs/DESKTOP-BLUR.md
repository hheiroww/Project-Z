# Shader desktop blur

Project-Z renders desktop blur through one HLSL shader compiled for native DX12,
Windows DX11 and DesktopGL. Windows DWM/accent blur and the extra acrylic companion
window are disabled. Window chrome still uses the operating system.

```csharp
var backdrop = sceneManager.DesktopBackdrop;
backdrop.Settings.Enabled = true;
backdrop.Settings.BlurRadius = 20; // Gaussian standard deviation in desktop logical pixels
backdrop.Settings.CaptureFramesPerSecond = 30;
backdrop.Settings.Saturation = 1.05f;
backdrop.Settings.NoiseOpacity = 0.006f;
scene.BackgroundColor = new Color(12, 16, 20) * 0.45f; // Background-only tint; controls stay sharp
```

The material uses a prefiltered, reduced-resolution desktop image and separable
Gaussian passes. Bilinear tap pairing covers 17 texels with nine texture reads
per axis. Padding around the client area prevents clipped blur at the edges.
The result is cached between capture updates. Saturation, tint and stable subtle
noise provide an acrylic-style finish; these are configurable rather than tied
to an OS theme. The DWM compositor's exact material/kernel is not a public API,
so this is a DWM-style approximation, not a promise of pixel identity or HDR parity.

Captured desktop pixels remain in bounded memory buffers. Project-Z neither
writes screenshots nor transmits them. Capture runs away from rendering, with
one outstanding capture operation per host. `State`, `StatusMessage`,
`CapturedFrameCount` and `LastCaptureMilliseconds` expose readiness and timing.

## Platform behavior

| Platform | Capture and exclusion | Supported renderer |
| --- | --- | --- |
| Windows 10 2004+ / Windows 11 | Windows Graphics Capture captures displays. `WDA_EXCLUDEFROMCAPTURE` omits each active blur host, revealing the windows underneath. The prior display affinity is restored when capture stops. | DX12, DX11, OpenGL |
| macOS 13+ | ScreenCaptureKit filters out the application's windows and supplies desktop frames. Screen Recording permission is required. | OpenGL |
| Linux X11 | XComposite window pixmaps are composed in stacking order through XRender, excluding the host's top-level frame and compositor overlay. The composited root is never sampled. | OpenGL |
| Linux Wayland / Xwayland within Wayland | The standard screen-cast portal has no exclusion contract for this material. Use a compositor-specific provider, or an X11 session. | Opaque fallback until safe exclusion exists |

On Windows, exclusion also hides the blurred host from other screenshots and
recordings while the feature is active. This is the OS-wide meaning of that
flag, not a capture-session setting. Disabling blur or disposing the scene manager
restores the previous flag. Protected/unavailable content and permission denial
produce an opaque fallback rather than a self-capture feedback loop.

On macOS, Project-Z shows a setup prompt before requesting Screen Recording
permission. The library cannot approve that OS permission on the user's behalf.
The native helper is universal (Intel and Apple Silicon); applications still own
their app signing and distribution entitlements.

## Managed Linux setup

When desktop blur is enabled and X11 support is missing, Project-Z shows the
package list and an **Install X11 components** button. Installation starts only
after that button is clicked. The distribution's package manager performs the
installation, and the OS polkit agent requests administrator authentication.
Project-Z never collects a password or runs a shell assembled from user input.
Ubuntu/Debian, Fedora and Arch-family recipes use their distribution repositories.
Other distributions keep the fallback and can provide their own setup UI.

Installing X11 client libraries alone cannot give a Wayland app access to hidden
desktop pixels. The session recipe also installs Xorg server/startup components;
the user must select a desktop environment's supported X11 session at the next
login. Some environments no longer provide an X11 session. Project-Z does not
replace the desktop environment, switch sessions, log the user out or restart a
display manager. The current Wayland session continues with the opaque fallback.

Applications can supply their own prompt by setting `ShowSetupPrompt = false`,
handling `Unavailable`, displaying `LinuxDesktopBlurSetup.CreatePlan(...)`, and
calling `InstallAsync(plan, confirmedByUser: true)` after the user's approval.
Successful library installation retries capture. A required session change is
reported explicitly and does not repeatedly invoke the installer.

## Compositor providers and embedded hosts

Set `CaptureSourceFactory` before enabling blur to provide an
`IDesktopCaptureSource`. Its `WindowBounds` and `DpiScale` describe desktop-space
coordinates; `TryCapture` fills an RGBA8 buffer for the padded requested bounds
and requested downsample factor. Reads run on a worker; window bounds are queried
on the game thread. The source must genuinely omit the host, including its
decorations. A black placeholder or an ordinary full-screen stream is inadequate.
Project-Z refuses providers whose `ExcludesHostWindow` is false.

For an embedded native host, set `NativeWindowHandle` to the visible owner before
enabling capture. Disable `Scene.UseDesktopBackdrop` on projection/off-screen
scenes; projection hosts do this automatically. Applications that need native
per-pixel click-through regions must handle those separately from the opaque
shader-backed client surface.

Existing `WindowAppearanceSettings.DwmBlurEnabled`, `WindowsComposition` material
requests and `DwmBackdropHost` calls route to this shader. The old
`DwmBackdropHost.Handle` now identifies its owner, since no companion is created;
the old color-key mask scan is a compatibility no-op. `Composition` no longer
reports a native DWM blur as enabled.

## Builds and source references

NuGet consumers receive the platform helper automatically. Source builds require
Visual C++ x64 tools and a Windows SDK with C++/WinRT on Windows, or Xcode's Swift
tools on macOS. Linux calls the installed X11 libraries directly. Native build
output stays under `obj`; it is packaged as a runtime asset rather than committed.
`BuildProjectZDesktopCapture=false` allows a managed-only source build, with the
opaque fallback when no compatible helper is supplied.

The shaders are `Resources/DesktopBlur.fx` and the intentionally versioned
DesktopGL binary. Rebuild the latter using `tools/Build-DesktopGLShaders.ps1` after
editing shader inputs. Native capture implementations live under
`Native/DesktopCapture`; the X11 provider is shared managed source.

Primary platform references:

- [Windows capture exclusion](https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-setwindowdisplayaffinity)
- [ScreenCaptureKit filters and capture](https://developer.apple.com/videos/play/wwdc2022/10156/)
- [Standard Wayland screen-cast portal options](https://flatpak.github.io/xdg-desktop-portal/docs/doc-org.freedesktop.portal.ScreenCast.html)
- [Ubuntu Xorg packages](https://packages.ubuntu.com/noble/xserver-xorg-core), [Fedora Xorg packages](https://packages.fedoraproject.org/pkgs/xorg-x11-server/xorg-x11-server-Xorg/), [Arch Xorg packages](https://archlinux.org/packages/extra/x86_64/xorg-server/)
