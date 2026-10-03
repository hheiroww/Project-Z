# Convert linked XAML, C# and VB views

The converter brings supported layout and code-behind behavior into Project-Z together. Named controls, initialization and event connections are generated for you. Your original source files remain unchanged; the output is built as a separate project.

Want this to happen during ordinary builds? Use the optional [Project-Z build-time setting](BUILD-TIME-XAML.md). The export commands below remain available.

## One view: select the XAML once

From the Project-Z source repository:

```powershell
dotnet run --project "Project Z XAML Codegen" -c Release -- import-view "C:\MyApp\View.xaml" --output "C:\Converted\View"
```

The command finds exactly one adjacent `View.xaml.cs` or `View.xaml.vb`. It also includes adjacent `View.Designer.cs/.vb` or `View.xaml.Designer.cs/.vb` partials, when present. If both language companions exist, choose explicitly. This importer is a repository tool, not a command installed by the main ProjectZ NuGet package.

For different filenames:

```powershell
dotnet run --project "Project Z XAML Codegen" -c Release -- import-view "C:\MyApp\Screen.xaml" --code-behind "C:\MyApp\ScreenLogic.vb" --designer "C:\MyApp\Screen.Designer.vb" --output "C:\Converted\Screen"
```

Use repeated `--source` options for additional source files. All files in one view must use the same language. If a VB view relies on its project's implicit root namespace, pass `--root-namespace MyApp`, or import the original `.vbproj` so that setting is read automatically. For dependencies, resources or project-wide settings, importing the complete project is the most reliable route.

The command creates a linked input project under `.projectz-input` and converted files under `Converted`. It automatically builds the converted view library. You can reference it from your Project-Z application and show the imported view as described in the [hosting guide](WPF-IMPORT.md). It does not invent an application startup flow for a single view.

## Existing project: linked files work too

Keep XAML as a `Page` item and code-behind or handwritten designer partials as `Compile` items. Visual Studio's **Add As Link** is supported; filenames do not have to sit inside the project folder. For example:

```xml
<ItemGroup>
  <Page Include="..\SharedViews\View.xaml" Link="Views\View.xaml" />
  <Compile Include="..\SharedViews\View.xaml.cs" Link="Views\View.xaml.cs" />
  <Compile Include="..\SharedViews\View.Designer.cs" Link="Views\View.Designer.cs" />
</ItemGroup>
```

Use `.vb` counterparts in a VB project. Then run:

```powershell
dotnet run --project "Project Z XAML Codegen" -c Release -- import "C:\MyApp\MyApp.csproj" --output "C:\Converted\MyApp"
```

The importer reads the evaluated project, including linked files and source project references. You do not need to copy linked files into the source project or manually wire supported controls and events. Adding links alone does not run conversion; the import command performs it and builds the result.

## What happens to designer code?

Project-Z regenerates the WPF-generated control fields and `InitializeComponent` from XAML. WPF `.g.cs`/`.g.vb` files and `obj`/`bin` outputs are not copied as application source. Handwritten designer partials remain source and are converted with the code-behind. Do not pass WinForms `Designer.cs` files: their forms and designer APIs are a different system. A custom designer that recreates WPF initialization itself may require adaptation rather than being interchangeable with generated WPF code.

## Repeating a conversion safely

Use the same command and destination to regenerate unchanged inputs. Original linked files are never rewritten. Changes to generated output are protected: conversion refuses to overwrite an edited generated file. Keep your additions in `Extensions/`. Use a new output directory when changing the single-view input list or its project settings.

## What is tested?

`tests/WpfImport/Verify.ps1` converts linked C# and VB XAML/code-behind/designer fixtures, repeats conversion, checks that the original files have identical hashes, and builds both results. Its native runtime fixture clicks each imported button and checks that the original handler runs exactly once and updates the named text control using a value from the designer partial. The runtime checks run on both DX12 and DX11.

Unsupported controls, APIs and markup produce diagnostics. A passing fixture is not a guarantee that every WPF application can convert unchanged; see the [support table](WPF-IMPORT.md#current-support).
