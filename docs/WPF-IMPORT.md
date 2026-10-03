# Import XAML and project code

Project-Z is the upstream framework. Applications depend on it; Project-Z, its compatibility library, generator, solution build, and default import tests have no dependency on a downstream application checkout.

The project importer preserves C# or VB and uses Roslyn symbols plus evaluated Release MSBuild inputs to redirect supported WPF APIs into `ProjectZ.WpfCompatibility`. Its adapters operate on native Project-Z controls. This is source compatibility, not a replacement for Microsoft's WPF assemblies or a binary rewriter.

## Convert a view and its code-behind together

For a single linked XAML/C#/VB view, use `import-view`. Matching code-behind and designer partials are found automatically. For an existing project, the `import` command below also includes files added with Add As Link. Both paths generate initialization and event wiring for supported features. See the [linked-view walkthrough](LINKED-XAML.md).

## Run

```powershell
dotnet run --project "Project Z XAML Codegen" -c Release -- import "C:\MyApp\MyApp.csproj" --output "C:\Converted\MyApp"
```

The output must be outside the source project directory. The converter imports source project references, preserves non-WPF packages and file references, emits language-matching designer partials, and compiles the converted root project. `App.xaml` produces a native Project-Z executable with a startup factory; view-library projects remain libraries. The host currently presents imported windows in one Project-Z scene rather than creating a separate operating-system window for each view.

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

- `Source/` contains converted source; `Generated/` contains initialization and event connections; `Xaml/` contains copied markup. Runtime content paths include the project identifier to prevent collisions between referenced projects.
- Put handwritten converted-project additions in `Extensions/`. They are compiled but never owned or rewritten by the importer.
- `projectz-import.manifest.json` records owned-file hashes. Regeneration is deterministic and refuses to overwrite edited or unowned output. Only unchanged stale owned files are removed.
- `projectz-import.sources.json` maps rewritten symbol spans to original source spans. `projectz-import.report.json` records success and file/line/column diagnostics. Unsupported code or markup produces a nonzero exit code and blocks the generated build.
- If MSBuild cannot load a dependency graph, the report includes the loading failure and a best-effort scan of evaluated XAML/project references. This is a failed import, not a successful application migration.

## Current support

| Area | Supported behavior |
| --- | --- |
| Source | C#, VB, partial classes, aliases, VB root namespaces, evaluated Release conditional compilation, recursive source project references |
| Initialization | Typed named fields, `InitializeComponent`, idempotent event attachment, VB `WithEvents`/`Handles`, explicit subscriptions, unnamed XAML handler targets |
| Controls | Window, UserControl, Page, Grid, StackPanel, Canvas, Border, Button, TextBlock, TextBox, CheckBox, ProgressBar, ListBox, ComboBox, Separator, Label, PasswordBox, RadioButton, Slider, WrapPanel, ScrollViewer, Expander, GroupBox |
| Events | Loaded/unloaded, size, focus, pointer, keyboard, text, selection, check state, window close; preview/bubble routing with handled input suppression |
| Properties | Registered and attached dependency properties, metadata defaults and callbacks; built-in text/content/check/value/items bridges |
| Binding | Inherited DataContext, dotted paths, nested replacement, explicit source, ElementName, relative self/ancestor, OneTime/OneWay/TwoWay, converters, lost-focus/property-change/explicit source updates |
| Collections | Observable item sources and fixed-height ListBox DataTemplates with per-item contexts and name scopes |
| Commands | ICommand, parameters, CanExecute-driven enablement |
| Lifetime | Binding and native-event cleanup, scene-thread dispatcher, async continuation synchronization, native application startup |

This is a bounded compatibility surface. Animation/transform markup, arbitrary effects, control templates, complex styles, dynamic resources, validation rules, MultiBinding, third-party WPF controls, custom dependency-property metadata, ComboBox item templates, and separate OS-window behavior are not implemented by this importer. Unsupported symbols and markup are diagnosed; no empty compatibility stubs are emitted. The older XAML-only parser may support visual features outside this new, stricter code-import surface.

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

This is not full WPF parity. Controls such as TreeView, DataGrid, DatePicker and Calendar, rich FlowDocument editing, arbitrary ControlTemplates, and the WPF animation/trigger system still require implementation. Unsupported import features continue to produce diagnostics.
