# Project Z technical reference

[Back to the showcase](../README.md)

## Graphics backends and 3D surfaces

MonoGame 3.8.5.1's native DirectX 12 backend is the default. It preserves Project Z's existing `Microsoft.Xna.Framework` drawing surface while moving the renderer to `MonoGame.Framework.Native` and `MonoGame.Runtime.Windows.DX12`.

The MonoGame WindowsDX backend remains available as a migration fallback:

```powershell
dotnet build "Project Z Windows.vbproj" -p:ProjectZGraphicsBackend=DirectX11
```

The native DX12 runtime is published for Windows x64 and ARM64. Project Z's Windows applications and VST3 host therefore target x64 by default.

### Native Windows composition and resizing

`WindowsComposition.Apply` resolves the real native HWND, enables borderless/resizable chrome, extends the DWM frame, and requests Acrylic or Mica with accent-blur fallback. `SceneManager` keeps the DX12 backbuffer and viewport synchronized with the client area and transforms native mouse coordinates while a resize is in progress.

Nested visibility and input follow the complete parent chain. Wheel input bubbles to parent controls, and `ScrollViewer` uses velocity, capped wheel/trackpad impulses, time-based deceleration, and edge stopping.

### heirowSnap Project-Z port

The solution includes `heirowSnap Project Z`, a native Project-Z/DX12 shell for the wShare client, plus `heirowSnap Protocol` for wire-compatible SocketJack models. It includes login/registration, file listing and streaming uploads, presence/chat, persistent clipboard history, Recycle Bin-confirmed deletion, drag-and-drop, tray behavior, Win+C toggling, desktop capture, transparent DWM composition, and migration of the existing `Documents\heirowSnap\heirowSnapConfig.json` identity into a separate Project-Z settings file.

Run the Release build with:

```powershell
dotnet run --project "heirowSnap Project Z\heirowSnap Project Z.vbproj" -c Release
```

The XAML importer maps the ported WPF surface—including `Canvas`, `WrapPanel`, `DockPanel`, `UniformGrid`, `Label`, `PasswordBox`, `RichTextBox`, `ToggleButton`, `Image`, `Popup`, `ContextMenu`, `MenuItem`, `MediaElement`, and kinetic `ScrollViewer` aliases—to native Project-Z equivalents. It also understands `ScrollViewer.Content`, `TabItem`, `DockPanel.Dock`, and common wShare root/control aliases.

### Inline video (Windows)

Project-Z's `MediaElement` now decodes video and synchronized audio through the Windows/WPF media engine on a dedicated STA dispatcher. It does not embed a WPF control or launch a player window: bounded, reusable pixel buffers feed the scene's MonoGame texture, including `Stretch`, clipping and opacity masks. Supported codecs match the Windows media engine. Decoding is capped at 30 frames/sec and `DecodePixelSize` limits texture dimensions.

```vb
Dim video As New MediaElement(scene) With {
    .Source = "C:\Media\clip.mp4", .Volume = 0, .Looping = True,
    .DecodePixelSize = 640, .Size = New Vector2(640, 360)}
scene.AddElement(video)
video.Play()
' video.Pause(), video.Seek(TimeSpan.FromSeconds(2)), video.Stop()
' MediaOpened / MediaEnded / MediaFailed events run on the scene thread.
```

heirowSnap generates missing video thumbnails asynchronously into the WPF-compatible `My Files\Thumb\<filename>_thumb.jpg` cache, repairs corrupt entries, and falls back to the first frame for clips shorter than one second. FFmpeg thumbnail jobs are serialized and thread-limited. Click a local video tile to play inline; further clicks toggle audio, clicking the progress strip seeks, and leaving the tile stops playback and restores its thumbnail. Only one tile plays at a time.

Every successful `Project Z VST` build automatically stages and deploys the complete plugin bundle to `C:\Program Files\Common Files\VST3\Project Z Granulizer Synth`. Windows requests administrator approval when needed. If FL Studio has the plugin loaded, the newest build is staged and installed automatically after FL Studio exits.

Create a procedural XYZW surface or import an OBJ/FBX model:

```vb
Imports Microsoft.Xna.Framework
Imports ProjectZ.Shared.Drawing.ThreeD
Imports ProjectZ.Shared.Drawing.UI.Advanced

Dim viewport As New Surface3DElement(Me) With {
    .Position = New Vector2(20, 20),
    .Size = New Vector2(640, 360),
    .Model = Model3DAsset.FromSurface(SurfaceMesh3D.CreateCube(2.0F)),
    .AutoRotateSpeed = New Vector3(0.25F, 0.5F, 0.0F)
}

' External and embedded diffuse textures are loaded with the model.
Await viewport.LoadModelAsync("Assets\model.fbx")
```

Assign a compiled MonoGame `Effect` to `ShaderEffect` for custom shaders. Project Z binds `World`, `View`, `Projection`, `WorldViewProjection`, and `DiffuseTexture` when those parameters exist; use `ConfigureShader` for additional uniforms.

## 🏗️ Solution Structure

```
Project-Z/
├── Project Z Windows.vbproj          # Core UI framework library (NuGet package)
│   ├── Shared/
│   │   ├── Drawing/
│   │   │   ├── Scene.vb              # Base scene class
│   │   │   ├── SceneManager.vb       # Scene orchestration
│   │   │   ├── UI/
│   │   │   │   ├── SceneElement.vb   # Base element class
│   │   │   │   ├── Primitives/       # Rectangle, Circle, Text, Polygon
│   │   │   │   ├── Input/            # Button, CheckBox, RadioButton, Textbox, Trackbar, ProgressBar
│   │   │   │   ├── Layout/           # Grid, StackPanel
│   │   │   │   └── Advanced/         # SpriteElement, PolygonElement, SceneProjectionHost
│   │   │   └── Designer/             # XAML parser & code generator
│   │   ├── Animations/               # DoubleAnimation, ColorAnimation, Timeline, Easing
│   │   ├── Content/                  # ContentContainer, Fonts, Textures, SpriteCollection
│   │   ├── Serialization/            # MetaSerializer, ObjectConverter
│   │   └── XNA/                      # SpriteBatchWrapper, SpriteBatchPropertySet
│   └── Extensions/                   # Color, Point, String, Dictionary helpers
│
├── Project Z Application/            # Demo app with playable Tetris
├── Project Z Audio/                  # Audio engine (NAudio, FFT, spectrum analysis)
├── Project Z Video FX/               # Visual effects demo
├── Project Z Tower Defense/          # Tower defense game & UI Designer
└── Project Z VST/                    # DirectX 12 VST3 granular synth and XAML-designed editor
```

## 🧩 UI Controls Reference

### Primitives

| Control | Description |
|---|---|
| `RectangleElement` | Filled rectangle with border, corner styling |
| `CircleElement` | GPU-rendered circle via mesh triangulation |
| `TextElement` | Rich text with wrapping modes (`NoWrap`, `Wrap`, `WrapWithOverflow`) |
| `PolygonElement` | Arbitrary polygon with LibTessDotNet mesh triangulation |
| `Surface3DElement` | GPU 3D viewport for XYZW surfaces, OBJ/FBX models, textures, and shaders |

### Rounded XAML surfaces

`RectangleElement`, `Border`, and `Button` support `CornerRadius = 12` or
`CornerRadii = New CornerRadii(12, 4, 8, 0)` (top-left, top-right, bottom-right,
bottom-left). Solid and gradient surfaces use antialiased, premultiplied-alpha
edges; radii fit the current layout bounds and scale with render transforms.

The XAML importer reads one/four-value `CornerRadius`, property elements,
resource/style values, and Border-root control templates. Clipboard item
containers can inherit their original template geometry through the optional
container argument of `ParseItemTemplate`. Discrete object keyframes preserve
all four radii. Existing VisualBrush image masks use the same corner geometry;
a rounded border does not implicitly clip arbitrary children (as in WPF).

Run the offline graphics regression with `heirowSnap.ProjectZ.exe
--corner-radius-smoke <output-directory>`. It writes `result.txt` and
`rounded-controls.png` without connecting to an account or installing hooks.

### XAML resource brushes and effects

Register the original application/theme dictionaries with
`SceneXamlParser.RegisterApplicationResources(path)` before importing windows.
Local resources override merged/application dictionaries. `BasedOn` styles,
solid-color resources, linear/radial gradient resources, brush opacity,
absolute mapping, gradient origins, and repeat/reflect spread modes are imported.
Buttons retain separate normal, hover, and pressed gradient brushes. Native
code-behind controls can consume linked styles through `ApplyAppearance`.
`XamlWindowMetrics` exposes original sizes, row heights, and margins to native hosts.

`BlurEffect`, `DropShadowEffect`, and chromatic-aberration effects have native
GPU adapters. Effects capture the control/subtree into reusable render targets;
ancestor scroll clipping and overlay order still apply. Radius, amount, color,
opacity, depth, direction, phase and angle can be targeted by imported storyboards.
`SpasticChromaticAbberationEffect`, `LinearChromaticAbberationEffect` and
`InvertEffect` now use the calculations from the original WPFPixelShaderLibrary
1.0.2 sources (including the spastic scan lines and full 0..1 Amount range).
Their source texture UVs remain stable while scrolling offscreen. See
`THIRD-PARTY-NOTICES.md` and the included GPL license for these adapters.
The generic chromatic adapter remains an RGB-offset approximation, and WPF's box
blur currently uses a Gaussian approximation. Unknown effects are reported in `UnsupportedFeatures`;
use `RegisterEffectAdapter` for explicit mappings, not silent WPF rendering.
This is not a converter for arbitrary compiled WPF `.ps` bytecode or a claim of
full WPF template/effect parity (e.g. DrawingBrush pattern layers and multi-triggers).

### heirowSnap desktop integration

The port links the original `wSettingsFile` and its supporting JSON classes in
`heirowSnap Legacy Settings`. `heirowSnapConfig.json` remains authoritative for
capture preferences and existing hotkeys. Its original changed-field merge/save
is retained, including preservation of unknown JSON fields. The new configurable
pause shortcut is a supplemental `PauseRecordingKey` in `heirowSnap.ProjectZ.json`.
Settings is a same-process Project-Z-rendered window, accessible from the tray
or the configured settings key. No settings companion executable is launched.
Advanced unsupported engine settings remain in JSON and are not silently reset.

Global shortcuts use the saved key/modifier combinations, not hardcoded defaults.
Modifier families, autorepeat and consumed key-up events are handled on a dedicated
hook thread. Key capture in Settings suppresses actions until the new binding is
saved. The custom tray menu includes live status-colored users, chat, capture,
pause, clipboard, settings and exit commands; it dismisses on deactivation or
pointer distance. The avatar and people icon containers have zero padding.

Notifications load the unchanged `Notification.xaml`, including load, hover and
click storyboards. Incoming messages open their conversation; upload/download
and capture completion notifications open the associated result. Expiration is
independent of animation clocks. Tool windows share the main DX12 device; a Win32
surface presents their rendered textures and forwards local input (no WPF controls).

Record video with the configured OpenKey (default Insert); screenshot with your
configured ScreenshotKey (configurable in Settings); audio-only with AudioKey
(default F1). FinishKey stops/saves or cancels selection (default Ctrl+Escape).
Pause defaults to Pause/Break and can be rebound. A click-through recording-region
border and a native Project-Z toolbar show elapsed time, audio level and
Pause/Stop actions. Capture controls request Windows capture exclusion.
Pausing closes a segment; resuming opens another. Finalization concatenates only
recorded time and muxes desktop audio, mixing microphone audio only when a device
is selected (the original -1/Disabled setting stays disabled). Failed-session intermediates are kept in
`Documents/heirowSnap/CaptureSessions`; successful intermediates are removed only
after producing the final file. No existing output file is overwritten.

Offline checks: `--desktop-smoke <directory> [--with-audio]` validates saved keys,
JSON merge behavior and a real paused/resumed recording; `--desktop-ui-smoke
<directory>` exercises native window mouse input, settings, tray and notifications,
plus the real recorder's Pause/Resume/Stop buttons and region-border state. It
does not connect to accounts, upload test captures, change the clipboard or
install global hooks.
`--corner-radius-smoke <directory>` also compares GPU distortion pixels with the
original shader formula. These are focused checks, not end-to-end remote message,
upload or streaming acceptance.

Database-backed `/Upload` responses already contain the registered owned-file
URL. The client does not repeat the obsolete disk-based `/Upload/Register`
check or consult stale MD5 sidecars; legacy root-alias responses still register
once. `--upload-contract-smoke <directory>` checks both contracts without a
server connection. Contact rows remain intact while pressed, and chat activation
is deferred until the source click finishes; chat HWND ownership keeps the
window above the topmost main window.

`Resources/XamlEffects.fx` compiles on build using the pinned local MGFXC tool
manifest, separately for DX12 and the DX11 fallback. Generated shader binaries
live under `obj`; edit shader source, never generated output. The offline
`--corner-radius-smoke` test also verifies resource brushes and GPU effects.

### Input Controls

| Control | Description |
|---|---|
| `Button` | Click-interactive button with auto-sizing and animation support |
| `CheckBox` | Three-state checkbox (checked, unchecked, indeterminate) |
| `RadioButton` | Grouped single-selection with automatic mutual exclusion |
| `Textbox` | Editable text input with alignment and padding |
| `Trackbar` | Value slider with tooltip, min/max range |
| `ProgressBar` | Determinate progress display |

### Layout Panels

| Control | Description |
|---|---|
| `Grid` | Row/column grid with `Auto`, `Star`, and `Fixed` sizing modes |
| `StackPanel` | Sequential layout in `Horizontal` or `Vertical` orientation |

## 🔧 Dependencies

| Package | Purpose |
|---|---|
| [MonoGame.Framework.Native](https://www.nuget.org/packages/MonoGame.Framework.Native/3.8.5.1) | XNA-compatible managed API for the native renderer |
| [MonoGame.Runtime.Windows.DX12](https://www.nuget.org/packages/MonoGame.Runtime.Windows.DX12/3.8.5.1) | Native DirectX 12 runtime (default) |
| [MonoGame.Framework.WindowsDX](https://www.nuget.org/packages/MonoGame.Framework.WindowsDX/3.8.5.1) | DirectX 11 migration fallback |
| [Silk.NET.Assimp](https://www.nuget.org/packages/Silk.NET.Assimp) | Assimp 6 native bindings for OBJ/FBX model import |
| [SocketJack](https://www.nuget.org/packages/SocketJack) | Networking & real-time data exchange |
| [LibTessDotNet](https://www.nuget.org/packages/LibTessDotNet) | MIT-licensed mesh triangulation for polygon rendering |

## 📋 Requirements

- **.NET 8.0** or later
- **Windows** (Windows Forms host)
- Visual Studio 2022+ recommended

## 🎮 Demo Projects

### Tetris (`Project Z Application`)
A fully playable Tetris game built entirely with Project Z UI elements — ghost pieces, next-piece preview, scoring, hard/soft drop, and pause support.

### Video FX (`Project Z Video FX`)
Real-time audio-reactive visualizer with spectrum analysis, loopback audio capture, and animated polygon effects.

### Tower Defense (`Project Z Tower Defense`)
A multiplayer tower defense game with real-time networking via SocketJack and a built-in WPF-hosted UI designer.

## 🤝 Contributing

Contributions are welcome! Feel free to open issues or submit pull requests.

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/my-feature`)
3. Commit your changes (`git commit -m 'Add my feature'`)
4. Push to the branch (`git push origin feature/my-feature`)
5. Open a Pull Request

## 📄 License

Copyright © 2014–2025. See the repository for license details.

Redistributions should include [`THIRD-PARTY-NOTICES.md`](../THIRD-PARTY-NOTICES.md).

---


