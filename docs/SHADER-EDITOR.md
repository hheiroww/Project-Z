# Shader editor

Open the native editor from a running Windows Project-Z application:

```csharp
var session = ProjectZ.Shaders.ShowEditor();
```

The **Project Z Application (Windows)** sample has a **Shader Editor** button. Its Tetris board shows the latest shader when you click **Run** in the editor. **Reset effect** restores the board's original appearance. The sample's **Theme** button switches the shared palette while both windows are open.

## Authoring

- **Simple** organizes shaders into Color and Alpha channel stacks. Add an input image, color or scalar value, gradient, checkerboard, or noise. Layers can be enabled, reordered, blended and adjusted independently. The inspector has Studio number controls and RGBA/hex color controls.
- **Advanced** opens the same graph on a node canvas. Search the palette or press **Space**, connect compatible ports, and wire the result to **XAML Output**. Use the wheel to zoom, right-drag to pan, **F** to frame, and **Alt-click** to disconnect an input. Math, channel, UV, time, procedural and custom HLSL expression nodes are available. Simple mode preserves advanced subgraphs it cannot represent as layers.
- **Presets** opens a searchable library of editable graphs. The 57 pixel-effect presets cover reflections, water, terrain, particles, and image effects. The eight 3D material presets open in Simple with editable channels. Use **Play time** to preview motion.
- **Run** or **Ctrl+Enter** compiles the shader and applies it to the native preview. Enable **Auto run** to compile after edits. Use **Load image** to provide a preview input and **Play time** for time-dependent effects. **Diagnostics** shows the compiler output.
- **Save** writes the editable `.pzshader` graph. **Save .fx** exports HLSL source. **Save .mgfx** compiles and exports bytecode for the running application's DirectX backend.

The editor uses the host's `WindowAppearance.Default` unless you pass a separate appearance controller to `ShowEditor`. Updates to its palette, background opacity and DWM blur are applied to an already-open editor. The shell, channel editor, and graph panels are native Project-Z XAML views.

## Apply a result at runtime

Consume the latest successful Run on the application's update or render thread:

```csharp
XamlNativeEffect effect = null;
if (session.TryTakeEffect(ref effect))
{
    var previous = element.VisualEffect;
    element.VisualEffect = effect;
    // Dispose the previous effect when this element owns it.
    previous?.Dispose();
}
```

`session.Close()` closes the editor when its owner is disposed. `session.Completion` reports shutdown or startup failure. The window close button and Alt+F4 prompt when the graph has unsaved changes. A host can pass an existing `.pzshader` path to `ShowEditor`.

## Reuse an authored graph

Use `session.TryTakeGraph(ref graphJson)` with a string initialized to `null` to consume the graph that produced the latest successful **Run**. Graph and visual-effect results can be consumed independently. The graph contains at most 256 KiB of UTF-8 JSON. An oversized graph can still produce an XAML effect.

`Shaders.ShowEditorForGraph(graphJson)` opens a draft of the graph. **Save** prompts for a destination. To compile the graph for a D3D11 video pipeline, pass `ShaderDocument.Parse(graphJson)` to `ProjectZ.ShaderEditor.ShaderGenerator.GenerateVideoShader`. The pixel entry point is `ProjectZVideoShader`. Supply the premultiplied source texture at `t0`, the sampler at `s0`, and a 16-byte `b0` constant buffer holding time, inverse texture dimensions and padding in that order. The output is premultiplied RGBA.

Use `ShowEditorForVideoGraph` or `ShowSurfaceEditorForGraph` when the graph belongs to a host project. **Apply** returns the graph for host validation. The host calls `AcknowledgeGraph` with the exact accepted string or `RejectGraph` with the validation message. On acknowledgment the editor marks that revision applied. **Export graph** writes a reusable copy.

## Author a real-time effect

Use `Shaders.ShowOwnedRealtimeShaderEditor(graphJson, appearance, ownerHandle, title)` for a modeless editor owned by the host window. Pass `null` to create a new graph. The preview plays a sample audio signal when **Play time** is active. **Apply to host** sends the editable graph to the host. Use `TryTakeGraph` to receive it, then call `AcknowledgeGraph` or `RejectGraph` with the validation result.

Compile `ShaderGenerator.GenerateRealtimeEffect(ShaderDocument.Parse(graphJson))` with `ShaderCompiler.CompileAsync` for the host's DirectX backend. Apply the compiled effect to a screen-space `SpriteBatch`. The input texture and output pixels use premultiplied alpha. Set `Time` to elapsed seconds and `TexelSize` to `(1 / width, 1 / height)` each frame. Set each scalar that the compiled `Effect` exposes.

- `AudioLevel`, `AudioLow`, `AudioMid`, `AudioHigh`, and `AudioBeat` take normalized audio levels from 0 to 1.
- `BeatPhase` is the fractional position within the beat. `Tempo` is beats per minute.
- `Macro01` through `Macro16` are normalized host controls. `ShaderRuntimeInputs.ScalarNames` provides the exact scalar names.

The node palette lists these scalars under **Runtime Input**. HLSL expression nodes can use the same names. The **Audio reactive** presets provide editable examples. Exported `.pzshader` graphs can be opened in the video pipeline, which supplies zero audio, phase and macro values and a 120 BPM tempo.

Keep `ProjectZ.dll`, `ProjectZ.ShaderEditor.dll`, the shader worker runtime configuration, and `ProjectZ.ShaderCompiler` beside the host component's managed dependencies. The worker uses a compatible component-local `.deps.json` when an unmanaged host opens it.

## Assign a 3D surface material

```csharp
var material = new SurfaceMaterial3D
{
    ShaderGraph = SurfaceMaterials.CreatePreset("clear-water").ToJson()
};
string source = material.GenerateShaderSource();
```

Use `ProjectZ.Shared.Drawing.ThreeD.SurfaceMaterial3D` to hold the graph and `ProjectZ.ShaderEditor.SurfaceMaterials` to create presets. The material validates and snapshots its graph on assignment. Its HLSL exposes `EvaluateSurfaceMaterial(uv, time)`, which returns linear base color, absorption and emission, roughness, metallic, transmission, index of refraction, normal strength and ambient occlusion. The caller's DirectX renderer supplies geometry, lighting, scene color and depth. A material without a graph uses the existing diffuse data.

Simple and Advanced edit the same nodes. When an Advanced connection drives a channel, Simple displays its linked status. Clear Water defaults to an index of refraction of 1.333 with transmission and ambient occlusion enabled. The other material presets cover Pool Water, Ocean Water, Murky Water, Glass, Glazed Ceramic, Brushed Metal and Matte Paint.

## Use an exported effect in XAML

Place the `.mgfx` next to the view loaded by `SceneXamlParser.ParseFile`, or copy it to the application's output directory. Use bytecode built for the same DirectX backend as the application.

```xml
<Border xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
    <Border.Effect>
        <CompiledShaderEffect Source="Effects/Tint.mgfx" />
    </Border.Effect>
</Border>
```

For code-created elements or generated XAML factories, assign `XamlNativeEffect.FromCompiledShader(File.ReadAllBytes(path))` to `VisualEffect`.

The Windows x64 editor ships with the pinned MonoGame compiler and uses the installed .NET 8 Desktop Runtime. Keep the Project-Z assemblies, worker runtime configuration, and `ProjectZ.ShaderCompiler` directory alongside the application.
