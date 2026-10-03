# Project-Z 2.7: setup and upgrade guide

## Choose the right starting point

- **Native interface:** continue using Project-Z scenes, controls and XAML with the default DX12 renderer. DX11 remains a source-build option.
- **HTML5 interface:** use the JavaScript scene engine in `Project Z HTML5`, then attach it to a desktop window with `Html5EngineHost`. The desktop host requires WebView2. Its page uses Canvas2D, not the native DirectX scene renderer.
- **Existing WPF application:** use the source importer and compatibility library from this repository. They convert supported project source and XAML into a separate output directory. They are separate source projects, not extra libraries bundled into the main ProjectZ package.

## Install the framework

```powershell
dotnet add package ProjectZ --version 2.7.0
```

The package contains the native framework and its model-import helper. HTML5 page assets are included as content files; your application must point the host at the deployed content directory. Start with the [HTML5 host example](../Project%20Z%20HTML5/README.md) to understand window and content setup.

## Move a supported WPF project

```powershell
dotnet run --project "Project Z XAML Codegen" -c Release -- import "C:\MyApp\MyApp.csproj" --output "C:\Converted\MyApp"
```

Keep the output separate from your original source. Read the generated diagnostics before treating the conversion as complete. Put manual additions in `Extensions/`; the importer refuses to overwrite edited files it previously generated. See the [WPF import guide](WPF-IMPORT.md) for supported controls, binding behavior and limitations.

## Text and control updates

Vector text draws font outlines, keeping letters smooth as size changes. Cursor placement and selection use the same measurements. Existing bitmap fonts remain a fallback. The controls and layout changes support imported views as well as native applications. See [text input](TEXT_INPUT.md) and [graphics quality](GRAPHICS_QUALITY.md) for settings and examples.

## Networking change to check

ProjectZ now depends on SocketJack 2026.15.0. If your application creates a `MutableTcpServer`, explicitly enable each required protocol before calling `Listen()`. SQL/TDS is local-only unless configured otherwise. Existing authentication requirements remain. Follow the [SocketJack upgrade guide](https://github.com/hheiroww/SocketJack/blob/master/docs/UPGRADING-2026.15.md).

## Verify an imported application

Run `pwsh -NoProfile -File tests/WpfImport/Verify.ps1` from the repository to exercise the supplied C#/VB fixtures on DX12 and DX11. These fixtures demonstrate supported behavior; test your application's own views, dependencies and workflows before deploying it.
