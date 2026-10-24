# Capture the native heirowSnap showcase

This helper loads the installed application in a separate process through a .NET
startup hook. It uses the application's actual DX12 renderer and DWM compositor,
stages twelve sample illustrations, scrolls the gallery and opens Graphics settings.
It does not contain application source or send mouse/keyboard input to other apps.

The profile, media and frames are isolated beneath a **new** output directory.
No account is loaded, no messages are sent, and existing desktop hosts are not
replaced. The colorful desktop runs in another process so the native compositor
can sample it. An unlocked Windows desktop, .NET 10 and the installed native app
are required. Keep the capture area clear of unrelated topmost windows.

```powershell
dotnet build tools/ShowcaseCapture/ShowcaseCapture.csproj -c Release
$env:DOTNET_STARTUP_HOOKS = (Resolve-Path tools/ShowcaseCapture/bin/Release/net10.0-windows/ShowcaseCapture.dll).Path
$env:PROJECTZ_SHOWCASE_OUTPUT = 'C:\Showcase\ProjectZ-capture'
try {
    Start-Process 'C:\Program Files\heirow\heirowSnap\heirowSnap.ProjectZ.exe' `
        -ArgumentList '--showcase-smoke' -WindowStyle Hidden -Wait
} finally {
    Remove-Item Env:\DOTNET_STARTUP_HOOKS
    Remove-Item Env:\PROJECTZ_SHOWCASE_OUTPUT
}
./tools/ShowcaseCapture/Encode.ps1 -CaptureFolder 'C:\Showcase\ProjectZ-capture' `
    -OutputFolder Images/showcase -FFmpeg 'ffmpeg'
```

The capture uses 16x SSAA, anisotropic 16x, 30 percent background opacity and
68 percent native blur. The GIF contains 90 successive live captures, plays at
10 frames per second and loops. Inspect the exported images and GIF before
publication; update the capture provenance when replacing them. Profiles, raw
frames, runtime logs and helper binaries are not publication assets.

The helper uses reflection against heirowSnap 26.10.64 and fails if an expected
application member changes. A failed or incomplete run is not exportable.
