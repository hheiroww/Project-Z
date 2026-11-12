# Shader editor

Open the native editor from a running Windows Project-Z application:

```csharp
var session = ProjectZ.Shaders.ShowEditor();
```

The **Project Z Application (Windows)** sample has a **Shader Editor** button. Its Tetris board shows the latest shader when you click **Run** in the editor. **Reset effect** restores the board's original appearance. The sample's **Theme** button switches the shared palette while both windows are open.

## Authoring

- **Simple** organizes shaders into Color and Alpha channel stacks. Add an input image, color or scalar value, gradient, checkerboard, or noise. Layers can be enabled, reordered, blended and adjusted independently. The inspector has Studio number controls and RGBA/hex color controls.
- **Advanced** opens the same graph on a node canvas. Search the palette or press **Space**, connect compatible ports, and wire the result to **XAML Output**. Use the wheel to zoom, right-drag to pan, **F** to frame, and **Alt-click** to disconnect an input. Math, channel, UV, time, procedural and custom HLSL expression nodes are available. Simple mode preserves advanced subgraphs it cannot represent as layers.
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

`session.Close()` closes the editor, and `session.Completion` reports shutdown or startup failure. A host can pass an existing `.pzshader` path to `ShowEditor`.

## Reuse an authored graph

Use `session.TryTakeGraph(ref graphJson)` with a string initialized to `null` to consume the graph that produced the latest successful **Run**. Graph and visual-effect results can be consumed independently. The graph contains at most 256 KiB of UTF-8 JSON. An oversized graph can still produce an XAML effect.

`Shaders.ShowEditorForGraph(graphJson)` opens a draft of the graph. **Save** prompts for a destination. To compile the graph for a D3D11 video pipeline, pass `ShaderDocument.Parse(graphJson)` to `ProjectZ.ShaderEditor.ShaderGenerator.GenerateVideoShader`. The pixel entry point is `ProjectZVideoShader`. Supply the premultiplied source texture at `t0`, the sampler at `s0`, and a 16-byte `b0` constant buffer holding time, inverse texture dimensions and padding in that order. The output is premultiplied RGBA.

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

The Windows x64 editor ships with the pinned MonoGame compiler and uses the installed .NET 8 Desktop Runtime. Keep the Project-Z assemblies, worker runtime configuration, and `ProjectZ.ShaderCompiler` directory alongside the application. Generated shaders target a Project-Z visual's 2D pixel effect, including its source texture, UVs and alpha. Unity material assets, vertex stages and render pipelines are outside that target.
