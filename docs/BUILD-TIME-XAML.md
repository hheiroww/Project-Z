# Build-time XAML conversion and Project-Z settings

Build-time conversion is **opt-in and off by default**. It supports C# and VB Windows applications targeting .NET 8 or .NET 10. It generates `View.design.cs` or `View.design.vb` under the project's `obj/<Configuration>/<TargetFramework>/ProjectZXaml` directory. Your original XAML, code-behind and manually ported designer files are never overwritten.

## Enable it in Visual Studio

Repository consumer projects get a **Project-Z** page under **Project Properties**. Select **Enable build-time XAML conversion** to save `ProjectZEnableXaml=true` in that project's file. Leave it unchecked for a manually ported application, including heirowSnap. Each project chooses independently.

Use **Excluded XAML files** for semicolon-separated paths or patterns, such as `Views\Manual\**\*.xaml`. Those views are not converted. Turning the feature off restores the project's normal compilation; it does not replace your code with a generated export.

For projects outside this repository, import the source tooling once:

```xml
<Import Project="C:\Source\Project-Z\Project Z XAML Codegen\ProjectZ.Xaml.targets" />
```

This integration currently comes from the source repository. Installing the main ProjectZ NuGet package alone does not install the converter or register this property page. Use a current .NET SDK and Visual Studio version supporting your target; the .NET 10 test solution is opened with VS 2026.

## Choose the XAML to build

Use `ProjectZXaml` items for linked or native-project views. Existing WPF `Page` items are picked up when the setting is enabled, except exclusions.

```xml
<PropertyGroup>
  <TargetFramework>net10.0-windows7.0</TargetFramework>
  <ProjectZEnableXaml>true</ProjectZEnableXaml>
  <ProjectZXamlExclude>Views\Manual\**\*.xaml</ProjectZXamlExclude>
</PropertyGroup>
<ItemGroup>
  <ProjectZXaml Include="..\SharedViews\View.xaml" />
  <Compile Include="..\SharedViews\View.xaml.cs" />
</ItemGroup>
```

Use `.vb` in a VB project. If source files are already included by the SDK's default file list, do not add duplicate `Compile` items. A linked file outside the project folder does need its `Compile` item.

Matching `.xaml.cs`/`.xaml.vb` and `.Designer.cs`/`.Designer.vb` files are selected automatically. For a differently named code-behind or a supporting source file that needs conversion, add `ProjectZXamlSource Include="path"` as well as its normal `Compile` item. Other source files continue compiling normally, including manually adapted Project-Z code.

## What an ordinary build does

1. Reads the selected XAML and source files.
2. Generates Project-Z initialization, named fields and event wiring as `.design.cs`/`.design.vb` files.
3. Compiles converted copies of the selected code-behind together with those generated partials.
4. Copies the view markup required at runtime to the output directory.

No separate import command is required after setup. Unchanged builds reuse the generated files. Editing a selected view regenerates it; unsupported markup fails the build with diagnostics. `Clean` removes generated files and the next build recreates them. When Visual Studio opens a configuration without generated files, the tooling runs an initial build to prepare the converted types for IntelliSense. Later design-time builds reuse that output. Generated files are build output, not files to edit by hand.

The process uses the same bounded WPF compatibility layer as the [source importer](WPF-IMPORT.md). It does not replace `App.xaml` startup or promise complete WPF/WinForms compatibility. Use the full project importer for an application startup conversion. Excluding a view leaves its normal build behavior intact, so existing WPF pages still require their normal WPF project setup.

## Preserve manual ports

For heirowSnap or another hand-adapted port, leave the checkbox off. If you later enable it for new views, select only those views and exclude existing ported markup. Keep manual changes in your own source files. Nothing in this feature writes generated code into the source tree.

## Test solution

Open `tests/WpfImport/BuildTime/ProjectZ.Xaml.BuildTests.sln` in VS 2026. The C# and VB `BuildTime` projects opt in; `Off` keeps a hand-edited designer and deliberately invalid XAML to prove conversion is bypassed. Set `Runtime` as the startup project to run both generated views.

```powershell
pwsh -NoProfile -File tests/WpfImport/BuildTime/Verify.ps1
# Run the views against .NET 10 explicitly:
dotnet run --project tests/WpfImport/BuildTime/Runtime/Runtime.csproj -c Release -p:ProjectZTestTargetFramework=net10.0-windows7.0
```

The settings page uses Visual Studio's documented [project property rule registration](https://github.com/dotnet/project-system/blob/main/docs/repo/property-pages/how-to-add-a-new-project-property-page.md).
