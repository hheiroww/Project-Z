# Your Project-Z XAML solution

Set **App** as the startup project. Open **Designer > MainWindow.xaml** and select **Design** or **Split** to use Visual Studio's WPF XAML designer. Edit the view and its event handlers in the Designer project, save, and press **F5** to run the native Project-Z app.

App links those files and translates the designer and code-behind during ordinary builds. It does not reference the Designer assembly or use WPF rendering. Generated files live under `App/obj`; edit the shared inputs in Designer instead. The first build needs the .NET 8 and .NET 10 SDKs and NuGet.org. No Project-Z source checkout is required.

Use App's **Project Properties > Project-Z** page to inspect build-time conversion settings. `DesignerAndCodeBehind` is enabled for this solution. Keep this mode when sharing WPF code-behind. The standard desktop template leaves conversion disabled until you opt in.

Choose **Debug/Release**, **Debug-DX11/Release-DX11**, or **Debug-OpenGL/Release-OpenGL** on App for DX12, DX11, or OpenGL. When using a command-line renderer override, clean first or use a separate output directory. Designer always uses the Windows XAML designer.

To convert an existing C# or VB WPF project and its project references into a separate native project tree:

```powershell
pwsh -File ./ProjectZ/Convert-Project.ps1 -SourceProject C:/Original/App.csproj -OutputDirectory C:/Converted/App
```

Run from App, or adjust the script path. Conversion preserves the original source and refuses to overwrite edited generated files. Review `projectz-import.report.json` and `projectz-import.unsupported.txt`. Unsupported features fail conversion and block generated builds; arbitrary third-party WPF controls and complex templates are not fully supported. Converted projects carry their build tools and renderer assemblies and can be moved to another machine.

The linked WPF code-behind is a `None` item in App and a `ProjectZXamlSource` conversion input. Only its generated native replacement compiles in App; this also prevents duplicate constructors and conflicting window types in IntelliSense. For an app created with an earlier template, change the linked `MainWindow.xaml.cs` or `.vb` item from `Compile` to `None`, replace `ProjectZ/ProjectZ.Xaml.targets` with this release's copy, and rebuild.

Keep the complete `ProjectZ` directory, notices and licenses in source control. The converter requires the installed .NET SDK; it never modifies the source project. Framework and tooling source: https://github.com/hheiroww/Project-Z
