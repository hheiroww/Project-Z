# Native Windows XAML and code-behind generation

Native scene applications can also compile their existing controls without the WPF compatibility layer. Reference `ProjectZ.Xaml.Build` and add `<ProjectZSceneXaml Include="UI/**/*.xaml" />` to the project. Normal `dotnet build`, Visual Studio builds, and publish compile the markup into the owning partial class under `obj`. There is no runtime markup file dependency and no application-specific parser or generator. The build package has no runtime dependencies and does not require a Project-Z source checkout.

Build the package with `dotnet pack "Project Z XAML Build/ProjectZ.Xaml.Build.csproj" -c Release -o artifacts/packages` and make it available through a local NuGet source. It is a separate build dependency from the graphics runtime package. No public package publication is implied by a local build.

Native markup uses `urn:projectz:native-scene` and a `SceneTemplates` root with the owning partial `Class`. Shared `Factory` declarations describe native control construction, setters, action events, and an optional attachment method. Page templates supply editable text, coordinates, sizes, and colors. `{Parameter name}` supplies a typed code-behind value or event callback; `{Resource name}` references a shared resource; `{Expression ...}` supplies a compiled C# formula for computed properties. Scalar strings, numbers, `#RRGGBB`/`#AARRGGBB` colors and `Vector2` pairs can be edited as literal values. `ObjectFactory` supports desktop controls with constructor arguments and typed deferred property values, preserving construction/property evaluation order. XAML expressions are trusted application source and are compiled as such. Unknown factories, properties, names and malformed values fail the build with source locations.

Templates marked `AutoCreate="true" Page="PageName"` can add controls without adding a C# call. The owning scene calls the generated `ProjectZCreateAdditionalControls` once after establishing its pages, supplying the ordinary attachment context. These controls cannot require runtime parameters. `ProjectZNativeXamlFontSizes` exposes literal `FontSize` values and named font sizes from markup so a native host can register vector fonts before building controls.

Native templates accept appearance overrides such as `Height`, `Font`, `TextPadding`, `CornerRadius`, colors, clipping and text wrapping. `consolidate-native-styles <source-directory> <xaml-directory>` folds exported constant appearance assignments into their owning XAML element and removes unused style resources, leaving the element's width, height and font authoritative. Runtime state styling and event callbacks remain in code-behind. The extraction regressions in `tests/NativeSceneExport/Verify.ps1` exercise local bindings and preserve constructor/property evaluation order.

The one-time `export-native-scene <source-directory> <factory-definitions.xaml>` command extracts factory call layouts and constant visual overrides into page XAML. Factories marked `Replace="true"` replace their old code constructors. `export-native-objects <project.csproj> <output-directory>` extracts native SceneElement and Windows Forms control initializers with semantic type information. Neither command is needed after extraction. Keep generated code under `obj`; edit the `.xaml` input. `tests/NativeSceneXaml/Verify.ps1` verifies compiled edits, action gating, native object bindings and build failures for invalid markup.

Use project import to translate an unchanged WPF application and its source project graph into a separate directory:

```powershell
dotnet run --project "Project Z XAML Codegen" -c Release -- import "C:\Original\Desktop\Desktop.csproj" --output "C:\Generated\Desktop" --root-directory Desktop
```

`--root-directory` is optional. It keeps the generated entry project at a stable folder name when the immutable input snapshot changes location. The importer preserves source files, protects edited or unowned output files, and removes only unchanged output files it previously generated. Compatibility fixes belong in Project-Z; regenerate application output after fixing them.

`projectz-import.report.json` contains source-located diagnostics and the generated root project path. `projectz-import.unsupported.txt` groups unsupported features and includes an example location for each. An import with diagnostics keeps the generated build blocked. Successful small fixtures do not establish compatibility for a larger application.

For an existing Project-Z Windows build, select `DesignerAndCodeBehind` and link the original files:

```xml
<PropertyGroup>
  <ProjectZXamlMode>DesignerAndCodeBehind</ProjectZXamlMode>
</PropertyGroup>
<ItemGroup>
  <Compile Include="../Original/View.xaml.cs" Link="View.xaml.cs" />
  <ProjectZXaml Include="../Original/View.xaml" />
  <ProjectZXamlSource Include="../Original/View.xaml.cs" />
</ItemGroup>
<Import Project="../Project Z XAML Codegen/ProjectZ.Xaml.targets" />
```

Mark additional WPF source or partial class files as `ProjectZXamlSource` when they also need translation. The build generates designers and translated source under `obj`, compiles those files, and copies generated runtime XAML. Application startup in `App.xaml` requires whole-project import.

The shared native parser supplies linear/radial background gradients, supported inherited style setters, rounded border surfaces, blur, and drop shadows. Code-behind brush/effect adapters update those native objects. Resource brushes preserve object identity, and shared effects use separate GPU surfaces for each visual. Arbitrary control templates, state triggers, shaders, and unlisted appearance options continue to produce diagnostics.

ComboBox and ListBox preserve original object items, scalar item-container content, `Tag`, selected objects, selection event items, display/value paths, and observable item changes. Data templates have independent namescopes. Names and resource keys are resolved by their XAML namespace URI, regardless of the XML prefix.

Windows codegen also maps DockPanel docking, Image/BitmapImage, ListViewItem, MenuItem/ContextMenu, basic control borders and typography, rectangle clipping, polygon geometry, and mutable rotation transforms. Text uses Project-Z's vector font renderer; underline, strike, overline, and baseline decorations render in the native layer. RichTextBox is a native input with an element host: document text becomes regular vector text and block UI containers become hosted elements. Complex document formatting currently becomes plain text.

Every native element exposes `Copy()` and `GetCopyText()`. Text and composite elements expose available text; elements without copyable text leave the clipboard unchanged. WPF clipboard interfaces map to the desktop clipboard service.

Open, save, and folder dialogs map to Project-Z-owned native windows with directory navigation, file filters, multiple file selection, save extensions, overwrite confirmation, and cancellation. They do not instantiate WPF or Windows common file dialogs.

Unsupported visual controls become named, empty layout elements, including visuals inside templates stored in resources. Their content descendants remain in the logical tree but are not drawn or hit tested. Their visual templates, item views, and visual-only properties are discarded. Codegen records these fallbacks in `projectz-import.empty-controls.txt`; unsupported nonvisual contracts and markup expressions still produce source-located build diagnostics. Regression input for these controls and dialogs is in `tests/WpfImport/ControlsCodegen/Original` and is compiled through both import and Windows codegen without edits. Text alignment maps to native left, center, and right alignment; justification currently uses left alignment. `ClipToBounds` uses native bounds clipping and preserves an independent rectangle geometry clip. `AutomationProperties.Name` and `AutomationProperties.HelpText` retain native adapter metadata, including bound values; this does not install a Windows UI Automation provider. CPU-only translation and clipping checks are in `tests/WpfImport/HeadlessMarkup`.

Simple `ControlTemplate` declarations with one Border around blank content presentation project background, border, padding, corner radii, and effects onto the native control. Same-property `TemplateBinding` values retain the control's native property. Scalar Button `IsMouseOver=True` and `IsPressed=True` Background triggers use native hover and pressed colors, including setters targeting that Border. Complex template layouts and other state triggers remain diagnostics.

The native object path also accepts GridView/GridViewColumn rows and vector text headers, inline ItemsPanelTemplate declarations, FlowDocument blocks and inlines, mutable scale/translation groups, and WindowChrome decoration metadata. WindowChrome rounds the native surface and retains caption/resize options; the ordinary window lifecycle and non-client HWND behavior have separate compatibility requirements. DataTrigger observes model notifications and applies/restores scalar setters; supported enter/exit storyboard actions use native clocks. Inline EventTrigger/BeginStoryboard declarations infer their owning element and retain begin delays, auto-reverse, speed, and Forever repetition. Blank visual targets do not run animations. Merged resource dictionaries share native resource resolution, with local resources taking precedence. Unsupported options inside recognized declarations remain diagnostics.

For a small read-only check without loading the source project graph or opening a graphics device, run `dotnet "Project Z XAML Codegen/bin/Release/net8.0/ProjectZ.XamlCodegen.dll" validate-xaml "C:/Original" --output "C:/Evidence/xaml-audit.json"`. The audit distinguishes missing XAML types from unsupported properties and template features. Zero missing types does not establish that the application builds or runs.

WPF WebView2 and WebView2CompositionControl references translate to Project-Z-owned adapters using heirowSnap's WinForms child-HWND host. The real WebView2 Core SDK remains available, including initialization, scripts, navigation, messages, and cancellable popup events. Native bounds, ancestor clipping/visibility, and disposal govern the child HWND. The compatibility Application pumps native messages while browser hosts are alive and retains the Project-Z dispatcher context; generated app code needs no manual message pump. Creation properties translate with the browser; unknown binary WPF contracts still produce diagnostics. This is a windowed browser surface, with the usual child-HWND airspace limitation, rather than WPF composition rendering.

Imported CLR events, attached event attributes, and handlers in inline or resource DataTemplates share the Project-Z SceneEvents router. Preview events tunnel, ordinary input/control events bubble, and direct lifecycle events retain their direct route. Source/OriginalSource, Handled, AddHandler/RemoveHandler, handledEventsToo, native mouse capture, right pointer input, and Unicode composition are carried through that router. EventTrigger storyboards use the same event identities in imported views. Template instances keep their own namescope and invoke the declaring view's codebehind handlers. Run `tests/WpfImport/WebViewEvents/Verify.ps1` for the unchanged-source import, Windows DesignerAndCodeBehind build, CPU checks, and one bounded native browser/input window; `-SkipRuntime` avoids opening a window.

`tests/WpfImport/Verify.ps1` covers unchanged C#/VB imports, Windows build generation, source/output preservation, unsupported diagnostics, actual native input and GPU rendering with DX12 and the explicit DX11 fallback. Native codegen regression source is in `tests/WpfImport/NativeCodegen/Original`; the Windows build links those unchanged files.
