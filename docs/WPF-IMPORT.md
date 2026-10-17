# Import XAML and project code

Project-Z is the upstream framework. Applications depend on it; Project-Z, its compatibility library, generator, solution build, and default import tests have no dependency on a downstream application checkout.

The project importer preserves C# or VB and uses Roslyn symbols plus evaluated Release MSBuild inputs to redirect supported WPF APIs into `ProjectZ.WpfCompatibility`. Its adapters operate on native Project-Z controls. This is source compatibility, not a replacement for Microsoft's WPF assemblies or a binary rewriter.

## Convert a view and its code-behind together

For a single linked XAML/C#/VB view, use `import-view`. Matching code-behind and designer partials are found automatically. For an existing project, the `import` command below also includes files added with Add As Link. Both paths generate initialization and event wiring for supported features. See the [linked-view walkthrough](LINKED-XAML.md).

## Run

```powershell
dotnet run --project "Project Z XAML Codegen" -c Release -- import "C:\MyApp\MyApp.csproj" --output "C:\Converted\MyApp"
```

The output must be outside the source project directory. The converter imports source project references, preserves non-WPF packages and file references, and emits language-matching designer partials and a root project to build. `App.xaml` produces a native Project-Z executable with a startup factory; view-library projects remain libraries. In the standalone host, each imported Window has its own Project-Z HWND, graphics device, and scene. Embedded imported windows use the application's existing scene.

To use an imported view inside an existing game, create a compatibility `Application` on the scene thread before constructing the view, then call `Show()` on an imported window. Dispose the compatibility application with the scene. It installs a synchronization context for async UI continuations and drains dispatcher work during scene drawing. The standalone host also drains it during update.

```csharp
using var application = new ProjectZ.WpfCompatibility.Application(scene);
var window = new ConvertedApplication.MainWindow();
window.Show();
// Continue the normal Project-Z update/draw loop.
```

MSBuild entry point:

```powershell
dotnet msbuild "Project Z XAML Codegen/ProjectZ.Import.proj" `
  -p:ProjectZImportSource="C:\MyApp\MyApp.csproj" `
  -p:ProjectZImportOutput="C:\Converted\MyApp"
```

The existing `<xaml-directory> <output-directory>` invocation remains available. Its legacy VB wrapper generator infers the namespace from the source directory's single VB project, falling back to `ProjectZImported`, and derives its legacy content subdirectory from the supplied input directory.

## Generated files and diagnostics

Imported application projects expose **Project Properties > Project-Z** in Visual Studio. The **Application startup** section lists imported windows with accessible parameterless constructors; **Preserve App.xaml startup** keeps the application's startup handler and factory. Choosing a specific window constructs that window instead. **Scene manager** configures the frame rate, initial/minimum viewport dimensions, resizing, hardware input, debug overlay, scene console, content directory, and clear color. Shutdown behavior is configurable alongside the main window.

The build generates a static `ProjectZGenerated.Application` in `obj/<configuration>/<framework>/ProjectZApplication/Application.cs` (a module in `Application.vb` for VB). The generated entry point uses this class to apply the settings before starting native scenes and windows. Configure it through the properties page; generation owns the source. The importer preserves changes to these startup properties across regeneration while still rejecting edits to generated application source or unrelated project settings. Existing native consumers can enable **Generate Application startup** and specify a fully qualified imported window type.

- `Source/` contains converted source; `Generated/` contains initialization and event connections; `Xaml/` contains copied markup. Runtime content paths include the project identifier to prevent collisions between referenced projects.
- Put handwritten converted-project additions in `Extensions/`. They are compiled but never owned or rewritten by the importer.
- `projectz-import.manifest.json` records owned-file hashes. Regeneration is deterministic and refuses to overwrite edited or unowned output. Only unchanged stale owned files are removed.
- `verify-import <output-directory>` rebuilds a codegen-owned import after a native runtime repair, updates its report/manifest, and restores the compilation guard if the build fails. It rejects outstanding translator diagnostics and modified generated source.
- `projectz-import.sources.json` maps rewritten symbol spans to original source spans. `projectz-import.report.json` records success and file/line/column diagnostics. Unsupported code or markup produces a nonzero exit code and blocks the generated build.
- If MSBuild cannot load a dependency graph, the report includes the loading failure and a best-effort scan of evaluated XAML/project references. This is a failed import, not a successful application migration.

## Current support

| Area | Supported behavior |
| --- | --- |
| Source | C#, VB, partial classes, aliases, VB root namespaces, evaluated Release conditional compilation, recursive source project references |
| Initialization | Typed named fields, `InitializeComponent`, idempotent event attachment, VB `WithEvents`/`Handles`, explicit subscriptions, unnamed XAML handler targets |
| Controls | Window, UserControl, Page, Grid, StackPanel, Canvas, Border, Button, TextBlock, TextBox, CheckBox, ProgressBar, ListBox, ComboBox, Separator, Label, PasswordBox, RadioButton, Slider, WrapPanel, ScrollViewer, Expander, GroupBox |
| Events | Loaded/unloaded, size, focus, pointer, keyboard, text, selection, check state, cancellable clipboard paste, window close; preview/bubble routing with handled input suppression |
| Properties | Registered and attached dependency properties, metadata defaults and callbacks; built-in text/content/check/value/items bridges |
| Binding | Inherited DataContext, dotted paths, nested replacement, explicit source, ElementName, relative self/ancestor, OneTime/OneWay/TwoWay, converters, lost-focus/property-change/explicit source updates |
| Collections | Observable item sources, filtered and stable sorted collection views, single/multiple/extended ListBox selection with original object identities, and fixed-height ListBox DataTemplates with per-item contexts and name scopes |
| Commands | ICommand, parameters, CanExecute-driven enablement |
| Lifetime | Binding and native-event cleanup, scene-thread dispatcher, async continuation synchronization, native application startup |

This is a bounded compatibility surface. Native geometry, mutable transforms, vector typography and text decorations, basic appearance, rich input with hosted elements, and Project-Z-owned file dialogs have automatic adapters. Unsupported visual controls become named blank elements; their templates and visual-only properties are discarded, and hidden content children retain their generated names. These substitutions are listed in `projectz-import.empty-controls.txt`. Missing nonvisual contracts, arbitrary control templates on supported controls, dynamic resources, validation rules, and MultiBinding still produce diagnostics. See [the codegen contract](../Project%20Z%20XAML%20Codegen/README.md) for the current implementation and limits.

Standalone windows support ownership, positioning, visibility, minimize/restore, modal owner disabling, and cancellable native close requests. HWND messages and imported input handlers use Project-Z's routed events. Native capture and bitmap encoding read the rendered window pixels. BitmapSource, mutable WriteableBitmap, cropped images, PNG/JPEG encoders, and BitmapImage file/stream decoding use native texture uploads. Pack and HTTP image URIs, automatic window sizing, and transparent native windows still require work.

Codebehind animations run on the target window's Project-Z frame clock. Double, thickness, and color animations support delay, easing, repeat, auto-reverse, pause/resume, completion, and restoring the base value. Mutable solid background/foreground brushes update actual native colors. Nested storyboards and full WPF composition semantics remain unsupported. Native state/data triggers restore layered setter values correctly; simple Border templates and input content hosts project their appearance and triggers into the native control. Decorative template parts project onto the native control surface; unavailable named decorations become empty. The importer records these projections alongside blank-control fallbacks. WPF virtualization, text rasterization, tooltip timing, and keyboard-navigation hints use native policy. Asymmetric border strokes project to the widest uniform native stroke. Optional drag adorners and path decorations use empty adapters. Content, commands, bindings, and event handlers retain their functional diagnostics.

ScrollViewer maps Disabled, Auto, Hidden, and Visible policies to its native viewport and tracks. Hidden preserves scrolling; Disabled resets and disables the axis; Visible retains a track when content fits. Native templates and data templates are constructed in their owning window scene, including resources shared between multiple graphics devices.

Clipboard text formats use the desktop clipboard. Native bitmap data converts to the imported BitmapSource contract, including image paste handlers and writes back to the desktop clipboard. DataObject.AddPastingHandler attaches to the native input's bubbling paste command: handlers see the original clipboard data, may replace its data/format, and may cancel insertion. Removing a handler restores the normal native paste path. Blank fallback controls keep shape coordinates, stroke caps/joins, stretch, grid dimensions, and child metadata without drawing or copying text.

Source conversion cannot change the WPF type contracts of a compiled third-party library. Supply its source or a native adapter. Build failures from unresolved source dependencies remain failures. This first implementation also does not reproduce arbitrary custom build targets, signing/publishing configuration, or legacy .NET Framework application packaging.

## Verification

```powershell
pwsh -NoProfile -File tests/WpfImport/Verify.ps1
```

The self-contained fixtures compile converted C# and VB projects, check source preservation and regeneration, reject unsupported imports, start a converted application, and exercise native controls under both DX12 and DX11. Results and screenshots are written under `artifacts/wpf-import/`.

External applications are optional test inputs, never framework dependencies:

```powershell
pwsh -NoProfile -File tests/WpfImport/VerifyExternalView.ps1 `
  -XamlPath "C:\ExternalApp\View.xaml" `
  -CodeBehindPath "C:\ExternalApp\View.xaml.vb"
```

For an expected compatibility rejection, pass `-ExpectedExitCode 1` and inspect the generated report. The external helper writes its temporary consumer project under artifacts and leaves the external sources unchanged.

### Additional native control adapters

The compatibility layer also imports `Separator`, `Label`, `PasswordBox`, `RadioButton`, `Slider`, `WrapPanel`, `ScrollViewer`, `Expander`, and `GroupBox`. These wrap the native Project-Z controls; password changes, range changes, radio selection, expansion events, and hosted content replacement are wired to native behavior. The runtime fixture checks these paths on both graphics backends. Slider orientation remains horizontal. Headers and label content currently accept text; hosted controls accept one FrameworkElement.

This is not full WPF parity. TreeView, DataGrid, DatePicker and Calendar currently use the blank-control fallback. Rich FlowDocument content projects to the native input's text and hosted elements; decorative ControlTemplates on supported controls project to their native surface rather than reproducing arbitrary WPF visual trees. Native editable dropdowns preserve typed text and item-container enabled bindings; double-click handlers route through Project-Z events.
