# Project Z HTML5

A dependency-free HTML5 runtime for Project Z. It keeps the engine's scene-first programming model while targeting the browser's Canvas2D API.

## Run inside Project-Z

```powershell
dotnet run --project "Project Z HTML5 Host/Project Z HTML5 Host.vbproj" -c Release
```

Project-Z creates the native MonoGame/DX12 window and embeds WebView2 as its child. The files in this directory are mapped to `https://project-z.local/` directly by WebView2; no HTTP server is started. `npm start` remains an optional browser-only development preview. Run the JavaScript regression suite with `npm test`.

Applications can attach the same host after their Project-Z window has been created:

```vb
Dim html = Html5EngineHost.Attach(Me, "path-to-html-content")
```

## Engine surface

- `ProjectZEngine`: responsive HiDPI canvas, fixed logical coordinates, frame timing, rendering, input routing and tween scheduling.
- `Scene` / `SceneElement`: nested transforms, z-order, visibility, opacity, lookup, ownership and reverse-order hit testing.
- `Rectangle`, `Circle`, `TextElement`, `Button`: reusable Canvas2D primitives and an interactive control.
- `StackPanel`: horizontal or vertical retained-mode layout.
- `Tween` / `Easing`: deterministic numeric property animation.
- `AssetStore`: cached asynchronous browser image loading.

The global `projectZ` object exposes the demo's `engine` and `scene` for browser-console inspection.

```js
import { Button, ProjectZEngine, Scene } from "./src/engine.js";

const engine = new ProjectZEngine(document.querySelector("canvas"), {
  width: 1280,
  height: 720
});
const scene = engine.setScene(new Scene({ background: "#06100e" }));
const button = new Button({ x: 40, y: 40, width: 180, height: 48, text: "Launch" });
button.onClick.on(() => console.log("launched"));
scene.add(button);
engine.start();
```

This is a browser engine target, not a claim of byte-for-byte API parity with the existing VB/MonoGame renderer. It intentionally leaves the native DX12/DX11 targets untouched.
