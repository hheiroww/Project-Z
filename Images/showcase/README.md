# Showcase capture provenance

The current README media was captured on **2026-10-24** from the installed native
**heirowSnap 26.10.64** application, build
`d5f4e9d09b32c4a7a98c48a63fa44a71cca8fd2c`.

| Asset | Content |
| --- | --- |
| `heirowsnap-glass.png` | Live file gallery beside the native Graphics editor, with a colorful desktop behind both windows. |
| `heirowsnap-gallery.png` | The current native gallery with twelve staged sample illustrations. |
| `heirowsnap-in-action.gif` | 90 successive live captures: gallery scrolling and opening Graphics over a moving desktop. |

The app rendered the actual UI using **16x SSAA**, **anisotropic 16x**, **30 percent
background opacity** and **68 percent native DWM blur**. Text and icons retain
their foreground opacity. The colorful desktop is a separate fixture process;
Windows applies its real compositor blur to that content. The capture toggles
blur after the main window becomes visible, then records the native glass.

The GIF loops at 10 frames per second and is resized to 1024 x 640 for delivery.
The PNGs retain the original 1440 x 900 capture size. No UI mockups, simulated
blur, account data, personal files or real messages are used. Sample illustrations
and the desktop artwork are generated locally by the capture helper.

[Reproduce the capture](../../tools/ShowcaseCapture/README.md). The offline profile,
raw frame sequence, logs and helper binaries are excluded from source publication.
These images demonstrate local rendering and UI, without asserting remote chat,
calling, uploading or streaming acceptance.

The other images in this folder are retained historical development captures.
`xaml-effects.png` is an offline DX12 graphics probe; the old settings and recording
images show earlier versions and are not used by the current README.
