using System.Diagnostics;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Text;
using ProjectZ.WpfCompatibility;
using CS = Microsoft.CodeAnalysis.CSharp;
using VB = Microsoft.CodeAnalysis.VisualBasic;

internal sealed class ProjectImporter
{
    internal sealed record Issue(string Code, string File, int Line, int Column, string Message);
    sealed record OwnedFile(string Path, string Hash);
    sealed record SourceMap(string Source, string Generated, int SourceStart, int SourceLength, int GeneratedStart, int GeneratedLength);
    readonly ConcurrentBag<Issue> issues = new();
    readonly List<SourceMap> mappings = new();
    readonly List<OwnedFile> owned = new();
    readonly Dictionary<string, string> previous = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<ProjectId, string> projects = new();
    string output = "", compatibility = "";
    static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";

    public static int Run(string[] args)
    {
        if (args.Length != 3 || args[1] != "--output") { Console.Error.WriteLine("Usage: ProjectZ.XamlCodegen import <csproj|vbproj> --output <directory>"); return 2; }
        var importer = new ProjectImporter();
        try
        {
            if (!MSBuildLocator.IsRegistered) MSBuildLocator.RegisterDefaults();
            return importer.Import(Path.GetFullPath(args[0]), Path.GetFullPath(args[2])).GetAwaiter().GetResult();
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error.GetBaseException().Message);
            if (importer.output.Length > 0) importer.RecordFailure(args[0], error);
            return 1;
        }
    }
    void RecordFailure(string input, Exception error)
    {
        try
        {
            issues.Add(new("PZI000", input, 1, 1, error.GetBaseException().Message));
            Write("ProjectZ.Import.targets", "<Project><Target Name=\"CheckProjectZImport\" BeforeTargets=\"CoreCompile\"><Error Text=\"Project-Z import failed; see projectz-import.report.json.\" /></Target></Project>");
            Write("projectz-import.report.json", JsonSerializer.Serialize(new { Success = false, RootProject = input, Issues = issues }, JsonOptions));
            var files = previous.Select(p => new OwnedFile(p.Key, p.Value)).Concat(owned).GroupBy(p => p.Path).Select(g => g.Last()).OrderBy(p => p.Path);
            File.WriteAllText(Path.Combine(output, "projectz-import.manifest.json"), JsonSerializer.Serialize(files, JsonOptions));
        }
        catch (Exception reportError) { Console.Error.WriteLine("Could not update failure report without overwriting user edits: " + reportError.Message); }
    }
    async Task<int> Import(string input, string destination)
    {
        if (!File.Exists(input)) throw new FileNotFoundException("Source project", input);
        if (Path.GetExtension(input) is not (".csproj" or ".vbproj")) throw new ArgumentException("A C# or VB project is required.");
        if (destination.Equals(Path.GetDirectoryName(input), StringComparison.OrdinalIgnoreCase) || destination.StartsWith(Path.GetDirectoryName(input)! + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Output must be outside the source project tree.");
        output = destination;
        var repository = new DirectoryInfo(AppContext.BaseDirectory);
        while (repository != null && !File.Exists(Path.Combine(repository.FullName, "Project Z Windows.vbproj"))) repository = repository.Parent;
        compatibility = Path.Combine(repository?.FullName ?? throw new DirectoryNotFoundException("Cannot locate the Project-Z compatibility project."), "Project Z WPF Compatibility", "ProjectZ.WpfCompatibility.csproj");
        Directory.CreateDirectory(output);
        var manifest = Path.Combine(output, "projectz-import.manifest.json");
        if (File.Exists(manifest)) foreach (var entry in JsonSerializer.Deserialize<List<OwnedFile>>(File.ReadAllText(manifest)) ?? []) previous.Add(entry.Path, entry.Hash);
        using var workspace = MSBuildWorkspace.Create(new Dictionary<string, string> { ["Configuration"] = "Release" });
        using var workspaceDiagnostics = workspace.RegisterWorkspaceFailedHandler(e => { if (e.Diagnostic.Kind == WorkspaceDiagnosticKind.Failure) issues.Add(new("PZI001", input, 1, 1, e.Diagnostic.Message)); });
        Project root;
        try { root = await workspace.OpenProjectAsync(input); }
        catch (Exception error)
        {
            issues.Add(new("PZI001", input, 1, 1, "MSBuild could not load the source dependency graph: " + error.GetBaseException().Message));
            await InspectXamlGraph(input, new(StringComparer.OrdinalIgnoreCase));
            Write("ProjectZ.Import.targets", "<Project><Target Name=\"CheckProjectZImport\" BeforeTargets=\"CoreCompile\"><Error Text=\"Project-Z import failed to load source dependencies; see projectz-import.report.json.\" /></Target></Project>");
            Write("projectz-import.report.json", JsonSerializer.Serialize(new { Success = false, RootProject = input, Issues = issues }, JsonOptions));
            File.WriteAllText(manifest, JsonSerializer.Serialize(owned.GroupBy(x => x.Path).Select(g => g.Last()), JsonOptions));
            Console.Error.WriteLine($"Project-Z source loading failed; {issues.Count} diagnostics saved to {Path.Combine(output, "projectz-import.report.json")}");
            return 1;
        }
        foreach (var project in root.Solution.Projects)
        {
            var key = Path.GetFileNameWithoutExtension(project.FilePath) + "-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(project.FilePath!)))[..8].ToLowerInvariant();
            projects[project.Id] = Path.Combine(output, key, Path.GetFileName(project.FilePath)!);
        }
        foreach (var project in root.Solution.Projects.OrderBy(p => p.FilePath, StringComparer.OrdinalIgnoreCase))
        {
            try { await ConvertProject(project); }
            catch (IOException) { throw; }
            catch (Exception error) { issues.Add(new("PZI004", project.FilePath!, 1, 1, error.GetBaseException().Message)); }
        }
        // Generated builds are gated by diagnostics. Never report an executable import while unsupported behavior remains.
        Write("ProjectZ.Import.targets", "<Project><Target Name=\"CheckProjectZImport\" BeforeTargets=\"CoreCompile\"><Error Condition=\"'$(ProjectZImportHasErrors)' == 'true'\" Text=\"Project-Z import has unsupported features; see projectz-import.report.json.\" /></Target></Project>");
        foreach (var project in projects.Values.Where(File.Exists))
        {
            var relative = Path.GetRelativePath(output, project);
            var xml = XDocument.Parse(File.ReadAllText(project));
            xml.Root!.AddFirst(new XElement("PropertyGroup", new XElement("ProjectZImportHasErrors", issues.Count != 0 ? "true" : "false")));
            Write(relative, xml.ToString());
        }
        if (issues.Count == 0)
        {
            var result = await Command("dotnet", ["build", projects[root.Id], "-c", "Release", "-p:GeneratePackageOnBuild=false", "--nologo", "-v:minimal"]);
            if (result.Code != 0) issues.Add(new("PZI900", input, 1, 1, "Generated compilation failed:\n" + result.Text));
        }
        if (issues.Count != 0) foreach (var path in projects.Values.Where(File.Exists))
        {
            var xml = XDocument.Load(path);
            foreach (var flag in xml.Descendants("ProjectZImportHasErrors")) flag.Value = "true";
            Write(Path.GetRelativePath(output, path), xml.ToString());
        }
        Write("projectz-import.report.json", JsonSerializer.Serialize(new { Success = issues.Count == 0, RootProject = projects[root.Id], Issues = issues.Distinct().OrderBy(i => i.File).ThenBy(i => i.Line).ThenBy(i => i.Column).ThenBy(i => i.Code) }, JsonOptions));
        Write("projectz-import.sources.json", JsonSerializer.Serialize(mappings, JsonOptions));
        // Remove only unmodified files previously owned by this importer.
        foreach (var stale in previous.Keys.Except(owned.Select(f => f.Path), StringComparer.OrdinalIgnoreCase))
        {
            var path = SafePath(stale);
            if (File.Exists(path) && Hash(File.ReadAllBytes(path)) == previous[stale]) File.Delete(path);
        }
        File.WriteAllText(manifest, JsonSerializer.Serialize(owned.GroupBy(x => x.Path).Select(g => g.Last()).OrderBy(x => x.Path), JsonOptions));
        foreach (var issue in issues.Distinct().Take(80)) Console.Error.WriteLine($"{issue.File}({issue.Line},{issue.Column}): error {issue.Code}: {issue.Message}");
        Console.WriteLine($"Project-Z import: {projects.Count} project(s), {issues.Count} diagnostic(s). Report: {Path.Combine(output, "projectz-import.report.json")}");
        return issues.Count == 0 ? 0 : 1;
    }
    static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    async Task InspectXamlGraph(string project, HashSet<string> seen)
    {
        if (!seen.Add(project)) return;
        var result = await Command("dotnet", ["msbuild", project, "-nologo", "-p:Configuration=Release", "-getItem:Page,ApplicationDefinition,ProjectReference"]);
        if (result.Code != 0) { issues.Add(new("PZI002", project, 1, 1, result.Text)); return; }
        try
        {
            using var json = JsonDocument.Parse(result.Text); var items = json.RootElement.GetProperty("Items");
            foreach (var kind in new[] { "Page", "ApplicationDefinition" }) foreach (var item in items.GetProperty(kind).EnumerateArray())
            {
                var path = item.GetProperty("FullPath").GetString()!;
                if (File.Exists(path)) ValidateXaml(path, XDocument.Load(path, LoadOptions.SetLineInfo));
            }
            foreach (var reference in items.GetProperty("ProjectReference").EnumerateArray())
            {
                var path = reference.GetProperty("FullPath").GetString()!;
                if (File.Exists(path)) await InspectXamlGraph(path, seen); else issues.Add(new("PZI002", project, 1, 1, "Missing source dependency " + path));
            }
        }
        catch (Exception error) { issues.Add(new("PZI002", project, 1, 1, error.GetBaseException().Message)); }
    }
    async Task ConvertProject(Project project)
    {
        var input = project.FilePath!;
        var directory = Path.GetDirectoryName(input)!;
        var target = Path.GetDirectoryName(projects[project.Id])!;
        var prefix = Path.GetRelativePath(output, target);
        bool vb = project.Language == LanguageNames.VisualBasic;
        var result = await Command("dotnet", ["msbuild", input, "-nologo", "-p:Configuration=Release", "-getProperty:RootNamespace,AssemblyName,TargetFramework,DefineConstants,OptionStrict,OptionInfer,OptionExplicit,Nullable,LangVersion,AllowUnsafeBlocks,UseWindowsForms", "-getItem:Page,ApplicationDefinition,Resource,Content,EmbeddedResource,PackageReference,Reference,Import"]);
        if (result.Code != 0) { issues.Add(new("PZI002", input, 1, 1, result.Text)); return; }
        using var evaluation = JsonDocument.Parse(result.Text);
        var properties = evaluation.RootElement.GetProperty("Properties");
        var items = evaluation.RootElement.GetProperty("Items");
        string Property(string name) => properties.TryGetProperty(name, out var value) ? value.GetString() ?? "" : "";
        var pages = new List<(string Path, XDocument Doc)>();
        foreach (var kind in new[] { "Page", "ApplicationDefinition" })
            foreach (var item in items.GetProperty(kind).EnumerateArray())
            {
                var path = item.GetProperty("FullPath").GetString()!;
                if (!File.Exists(path)) continue;
                var doc = XDocument.Load(path, LoadOptions.SetLineInfo); pages.Add((path, doc)); ValidateXaml(path, doc);
            }
        var compilation = await project.GetCompilationAsync() ?? throw new InvalidOperationException("Cannot compile source model.");
        var compatibilityDll = Path.Combine(Path.GetDirectoryName(compatibility)!, "bin", "Release", "net8.0-windows7.0", "ProjectZ.WpfCompatibility.dll");
        Compilation? adapterModel = File.Exists(compatibilityDll) ? compilation.AddReferences(MetadataReference.CreateFromFile(compatibilityDll)) : null;
        // Supply the normal WPF designer surface for semantic analysis when the project has never been built.
        foreach (var page in pages)
        {
            string? className = (string?)page.Doc.Root?.Attribute(X + "Class");
            if (className == null || (!Capabilities.Controls.Contains(page.Doc.Root!.Name.LocalName) && page.Doc.Root.Name.LocalName != "Application")) continue;
            var fullName = vb && Property("RootNamespace").Length > 0 ? Property("RootNamespace") + "." + className : className;
            var type = compilation.GetTypeByMetadataName(fullName);
            if (type?.GetMembers("InitializeComponent").Length > 0) continue;
            var text = page.Doc.Root.Name.LocalName == "Application" ? ApplicationDesigner(className, vb, "", null, true, true) : Designer(page.Doc, className, vb, "", original: true, type?.BaseType?.SpecialType != SpecialType.System_Object && type?.BaseType != null);
            compilation = compilation.AddSyntaxTrees(vb ? VB.VisualBasicSyntaxTree.ParseText(text, (VB.VisualBasicParseOptions?)project.ParseOptions) : CS.CSharpSyntaxTree.ParseText(text, (CS.CSharpParseOptions?)project.ParseOptions));
        }
        var compileFiles = new List<string>();
        var contentFiles = new List<string>();
        foreach (var tree in compilation.SyntaxTrees.Where(t => !string.IsNullOrEmpty(t.FilePath) && !IsGenerated(t.FilePath)).OrderBy(t => t.FilePath))
        {
            var relative = SourceRelative(directory, tree.FilePath);
            var generated = Path.Combine(prefix, "Source", relative);
            var model = compilation.GetSemanticModel(tree);
            var changes = new List<TextChange>();
            var root = await tree.GetRootAsync();
            Visit(root);
            void Visit(SyntaxNode node)
            {
                if (node is CS.Syntax.IdentifierNameSyntax or VB.Syntax.IdentifierNameSyntax)
                {
                    var member = model.GetSymbolInfo(node).Symbol;
                    if (member is IMethodSymbol or IPropertySymbol or IFieldSymbol or IEventSymbol && Capabilities.IsWpfAssembly(member.ContainingAssembly?.Name))
                    {
                        var ownerName = member.ContainingType.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
                        ownerName = ownerName switch { "System.Windows.Threading.DispatcherObject" => "System.Windows.DependencyObject", "System.Windows.Controls.Primitives.ButtonBase" => "System.Windows.Controls.Button", "System.Windows.Controls.Primitives.ToggleButton" => "System.Windows.Controls.CheckBox", "System.Windows.Controls.Primitives.RangeBase" => "System.Windows.Controls.ProgressBar", "System.Windows.Controls.Primitives.Selector" or "System.Windows.Controls.ItemsControl" => "System.Windows.Controls.ListBox", _ => ownerName };
                        var adapter = adapterModel?.GetTypeByMetadataName(Capabilities.Translate(ownerName));
                        bool found = adapterModel == null;
                        for (var type = adapter; type != null; type = type.BaseType) if (type.GetMembers(member.Name).Length > 0) { found = true; break; }
                        if (!found) Add("PZI102", tree.FilePath, node, $"Unsupported WPF member {ownerName}.{member.Name}");
                    }
                }
                bool name = node is CS.Syntax.NameSyntax or VB.Syntax.NameSyntax;
                if (name)
                {
                    var symbol = model.GetSymbolInfo(node).Symbol;
                    if (symbol is IAliasSymbol alias) symbol = alias.Target;
                    string? replacement = null;
                    if (symbol is INamedTypeSymbol named && Capabilities.IsWpfAssembly(named.ContainingAssembly?.Name))
                    {
                        var typeName = named.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
                        if (!Capabilities.Types.Contains(typeName)) Add("PZI101", tree.FilePath, node, "Unsupported WPF type " + typeName);
                        replacement = (vb ? "Global." : "global::") + Capabilities.Translate(typeName);
                    }
                    else if (symbol is INamedTypeSymbol command && command.ToDisplayString() == "System.Windows.Input.ICommand") replacement = (vb ? "Global." : "global::") + "System.Windows.Input.ICommand";
                    else if (symbol is INamespaceSymbol ns && ns.ToDisplayString().StartsWith("System.Windows", StringComparison.Ordinal) && !ns.ToDisplayString().StartsWith("System.Windows.Forms", StringComparison.Ordinal)) replacement = Capabilities.Translate(ns.ToDisplayString());
                    if (replacement != null) { changes.Add(new(node.Span, replacement)); return; }
                }
                foreach (var child in node.ChildNodes()) Visit(child);
            }
            var original = await tree.GetTextAsync();
            int delta = 0;
            foreach (var change in changes.OrderBy(c => c.Span.Start)) { mappings.Add(new(tree.FilePath, generated, change.Span.Start, change.Span.Length, change.Span.Start + delta, change.NewText!.Length)); delta += change.NewText.Length - change.Span.Length; }
            Write(generated, original.WithChanges(changes).ToString()); compileFiles.Add(Path.Combine("Source", relative));
        }
        foreach (var page in pages)
        {
            var relative = SourceRelative(directory, page.Path);
            var xamlOutput = Path.Combine("Xaml", relative);
            Write(Path.Combine(prefix, xamlOutput), page.Doc.ToString()); contentFiles.Add(xamlOutput);
            var className = (string?)page.Doc.Root?.Attribute(X + "Class");
            if (className == null || (!Capabilities.Controls.Contains(page.Doc.Root!.Name.LocalName) && page.Doc.Root.Name.LocalName != "Application")) continue;
            var designer = Path.Combine("Generated", relative + (vb ? ".g.vb" : ".g.cs"));
            var fullName = vb && Property("RootNamespace").Length > 0 ? Property("RootNamespace") + "." + className : className;
            var originalType = (await project.GetCompilationAsync())?.GetTypeByMetadataName(fullName);
            bool sourceBase = originalType?.DeclaringSyntaxReferences.Any(r => !IsGenerated(r.SyntaxTree.FilePath) && (r.GetSyntax() is CS.Syntax.ClassDeclarationSyntax c && c.BaseList != null || r.GetSyntax().Parent is VB.Syntax.ClassBlockSyntax b && b.Inherits.Count > 0)) == true;
            bool sourceConstructor = originalType?.InstanceConstructors.Any(c => c.DeclaringSyntaxReferences.Any(r => !IsGenerated(r.SyntaxTree.FilePath))) == true;
            if (page.Doc.Root.Name.LocalName == "Application")
            {
                var startupUri = (string?)page.Doc.Root.Attribute("StartupUri");
                var startup = startupUri == null ? null : pages.FirstOrDefault(p => p.Path.Equals(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(page.Path)!, startupUri)), StringComparison.OrdinalIgnoreCase)).Doc;
                string? startupClass = (string?)startup?.Root?.Attribute(X + "Class");
                if (startupUri != null && startupClass == null) issues.Add(new("PZI302", page.Path, 1, 1, "StartupUri does not resolve to an imported Window: " + startupUri));
                if (vb && startupClass != null && Property("RootNamespace").Length > 0) startupClass = Property("RootNamespace") + "." + startupClass;
                Write(Path.Combine(prefix, designer), ApplicationDesigner(className, vb, prefix + "/" + xamlOutput.Replace('\\', '/'), startupClass, false, sourceConstructor, page.Doc.Root));
                var main = Path.Combine("Generated", vb ? "ProjectZEntry.vb" : "ProjectZEntry.cs");
                Write(Path.Combine(prefix, main), vb ? $"Friend Module ProjectZEntry\n<System.STAThread>\nPublic Sub Main(args As String())\nGlobal.{Capabilities.Prefix}.Hosting.Run(Function() New Global.{fullName}(), args)\nEnd Sub\nEnd Module" : $"internal static class ProjectZEntry {{ [System.STAThread] public static void Main(string[] args) => global::{Capabilities.Prefix}.Hosting.Run(() => new global::{fullName}(), args); }}");
                compileFiles.Add(main);
            }
            else Write(Path.Combine(prefix, designer), Designer(page.Doc, className, vb, (prefix + "/" + xamlOutput.Replace('\\', '/')), false, sourceBase, sourceConstructor));
            compileFiles.Add(designer);
        }
        var projectXml = new XElement("Project", new XAttribute("Sdk", "Microsoft.NET.Sdk"));
        var props = new XElement("PropertyGroup", new XElement("TargetFramework", string.IsNullOrWhiteSpace(Property("TargetFramework")) ? "net8.0-windows7.0" : Property("TargetFramework")), new XElement("OutputType", "Library"), new XElement("EnableDefaultCompileItems", "false"), new XElement("EnableDefaultEmbeddedResourceItems", "false"), new XElement("EnableDefaultContentItems", "false"));
        foreach (var name in new[] { "RootNamespace", "AssemblyName", "DefineConstants", "OptionStrict", "OptionInfer", "OptionExplicit", "Nullable", "LangVersion", "AllowUnsafeBlocks", "UseWindowsForms" }) if (name == "RootNamespace" || Property(name).Length > 0) props.Add(new XElement(name, Property(name)));
        projectXml.Add(props);
        if (pages.Any(p => p.Doc.Root?.Name.LocalName == "Application"))
        {
            props.SetElementValue("OutputType", "WinExe"); props.Add(new XElement("StartupObject", vb && Property("RootNamespace").Length > 0 ? Property("RootNamespace") + ".ProjectZEntry" : "ProjectZEntry"));
        }
        var includes = new XElement("ItemGroup");
        foreach (var file in compileFiles) includes.Add(new XElement("Compile", new XAttribute("Include", file)));
        includes.Add(new XElement("Compile", new XAttribute("Include", "Extensions/**/" + (vb ? "*.vb" : "*.cs"))));
        foreach (var file in contentFiles) includes.Add(new XElement("Content", new XAttribute("Include", file), new XElement("TargetPath", Path.Combine(prefix, file)), new XElement("CopyToOutputDirectory", "PreserveNewest")));
        includes.Add(new XElement("ProjectReference", new XAttribute("Include", Path.GetRelativePath(target, compatibility))));
        if (pages.Any(p => p.Doc.Root?.Name.LocalName == "Application")) includes.Add(new XElement("Content", new XAttribute("Include", Path.GetRelativePath(target, Path.Combine(Path.GetDirectoryName(compatibility)!, "..", "Project Z Application", "Content", "Fonts", "Segoe UI", "*.xnb"))), new XElement("Link", "Content/Fonts/Segoe UI/%(Filename)%(Extension)"), new XElement("CopyToOutputDirectory", "PreserveNewest")));
        foreach (var reference in project.ProjectReferences) if (projects.TryGetValue(reference.ProjectId, out var path)) includes.Add(new XElement("ProjectReference", new XAttribute("Include", Path.GetRelativePath(target, path))));
        foreach (var package in items.GetProperty("PackageReference").EnumerateArray())
        {
            var name = package.GetProperty("Identity").GetString()!;
            if (name.StartsWith("Microsoft.NET", StringComparison.Ordinal)) continue;
            var version = package.TryGetProperty("Version", out var v) ? v.GetString() : null;
            if (string.IsNullOrEmpty(version)) { issues.Add(new("PZI003", input, 1, 1, $"Cannot resolve package version: {name}")); continue; }
            includes.Add(new XElement("PackageReference", new XAttribute("Include", name), new XAttribute("Version", version)));
        }
        if (vb) foreach (var import in items.GetProperty("Import").EnumerateArray()) includes.Add(new XElement("Import", new XAttribute("Include", Capabilities.Translate(import.GetProperty("Identity").GetString()!))));
        foreach (var reference in items.GetProperty("Reference").EnumerateArray())
        {
            var identity = reference.GetProperty("Identity").GetString()!;
            if (Capabilities.IsWpfAssembly(identity.Split(',')[0])) continue;
            if (!reference.TryGetProperty("HintPath", out var hint) || string.IsNullOrWhiteSpace(hint.GetString())) continue;
            var source = Path.GetFullPath(Path.Combine(directory, hint.GetString()!));
            if (!File.Exists(source)) { issues.Add(new("PZI203", input, 1, 1, "Missing binary reference " + source)); continue; }
            var relative = Path.Combine("References", Path.GetFileName(source));
            WriteBytes(Path.Combine(prefix, relative), File.ReadAllBytes(source));
            includes.Add(new XElement("Reference", new XAttribute("Include", identity), new XElement("HintPath", relative)));
        }
        foreach (var kind in new[] { "Content", "Resource", "EmbeddedResource" })
            foreach (var item in items.GetProperty(kind).EnumerateArray())
            {
                var path = item.GetProperty("FullPath").GetString()!; if (!File.Exists(path) || path.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase)) continue;
                var relative = Path.Combine("Assets", SourceRelative(directory, path));
                WriteBytes(Path.Combine(prefix, relative), File.ReadAllBytes(path));
                includes.Add(new XElement(kind == "EmbeddedResource" ? "EmbeddedResource" : "Content", new XAttribute("Include", relative), new XElement("CopyToOutputDirectory", "PreserveNewest")));
            }
        projectXml.Add(includes, new XElement("Import", new XAttribute("Project", "../ProjectZ.Import.targets")));
        Write(Path.GetRelativePath(output, projects[project.Id]), projectXml.ToString());
        ScanBinaryContracts(compilation, project);
    }
    void ScanBinaryContracts(Compilation compilation, Project project)
    {
        foreach (var reference in compilation.References.OfType<PortableExecutableReference>())
        {
            if (compilation.GetAssemblyOrModuleSymbol(reference) is not IAssemblySymbol assembly) continue;
            var name = assembly.Name;
            if (reference.FilePath?.Contains("\\packs\\", StringComparison.OrdinalIgnoreCase) == true || project.Solution.Projects.Any(p => p.AssemblyName == name)) continue;
            if (Capabilities.IsWpfAssembly(name) || name.StartsWith("System") || name.StartsWith("Microsoft") || name is "mscorlib" or "netstandard") continue;
            void Walk(INamespaceOrTypeSymbol container)
            {
                foreach (var member in container.GetMembers())
                {
                    if (member is INamespaceSymbol ns) Walk(ns);
                    else if (member is INamedTypeSymbol type && type.DeclaredAccessibility == Accessibility.Public)
                    {
                        if (type.BaseType != null && Capabilities.IsWpfAssembly(type.BaseType.ContainingAssembly.Name)) issues.Add(new("PZI201", project.FilePath!, 1, 1, $"Binary {name}: {type} inherits {type.BaseType}; import its source or supply a native adapter."));
                        foreach (var api in type.GetMembers().Where(m => m.DeclaredAccessibility == Accessibility.Public))
                        {
                            IEnumerable<ITypeSymbol> types = api switch { IMethodSymbol m => m.Parameters.Select(p => p.Type).Append(m.ReturnType), IPropertySymbol p => [p.Type], IFieldSymbol f => [f.Type], IEventSymbol e => [e.Type], _ => [] };
                            if (types.Any(t => Capabilities.IsWpfAssembly(t.ContainingAssembly?.Name))) issues.Add(new("PZI202", project.FilePath!, 1, 1, $"Binary {name}: {api} exposes WPF types; source conversion cannot rewrite this binary contract."));
                        }
                    }
                }
            }
            Walk(assembly.GlobalNamespace);
        }
    }
    void ValidateXaml(string path, XDocument document)
    {
        foreach (var diagnostic in XamlSupport.Validate(document))
            issues.Add(new("PZI301", path, diagnostic.Line, diagnostic.Column, diagnostic.Message));
    }
    static string ApplicationDesigner(string className, bool vb, string path, string? startupClass, bool original, bool hasConstructor, XElement? root = null)
    {
        int split = className.LastIndexOf('.'); string ns = split < 0 ? "" : className[..split], name = split < 0 ? className : className[(split + 1)..];
        var prefix = original ? "System.Windows" : Capabilities.Prefix;
        var sb = new StringBuilder();
        if (vb)
        {
            if (ns.Length > 0) sb.AppendLine("Namespace " + ns);
            sb.AppendLine($"Partial Public Class [{name}]\nInherits Global.{prefix}.Application");
            if (!original && !hasConstructor) sb.AppendLine("Public Sub New()\nInitializeComponent()\nEnd Sub");
            sb.AppendLine("Private _projectZInitialized As Boolean\nPrivate Sub InitializeComponent()\nIf _projectZInitialized Then Return\n_projectZInitialized = True");
            if (!original)
            {
                sb.AppendLine($"Global.{prefix}.XamlLoader.InitializeApplication(Me, \"{path.Replace("\"", "\"\"")}\")");
                if (startupClass != null) sb.AppendLine($"StartupFactory = Function() New Global.{startupClass}()");
                foreach (var attr in root?.Attributes().Where(a => a.Name.LocalName is "Startup" or "Exit") ?? []) sb.AppendLine($"AddHandler Me.{attr.Name.LocalName}, AddressOf Me.[{attr.Value}]");
            }
            sb.AppendLine("End Sub\nEnd Class"); if (ns.Length > 0) sb.AppendLine("End Namespace");
        }
        else
        {
            if (ns.Length > 0) sb.AppendLine("namespace " + ns + " {");
            sb.AppendLine($"public partial class @{name} : global::{prefix}.Application {{");
            if (!original && !hasConstructor) sb.AppendLine($"public @{name}() {{ InitializeComponent(); }}");
            sb.AppendLine("private bool _projectZInitialized; private void InitializeComponent() { if (_projectZInitialized) return; _projectZInitialized = true;");
            if (!original)
            {
                sb.AppendLine($"global::{prefix}.XamlLoader.InitializeApplication(this, {JsonSerializer.Serialize(path)});");
                if (startupClass != null) sb.AppendLine($"StartupFactory = () => new global::{startupClass}();");
                foreach (var attr in root?.Attributes().Where(a => a.Name.LocalName is "Startup" or "Exit") ?? []) sb.AppendLine($"this.{attr.Name.LocalName} += this.@{attr.Value};");
            }
            sb.AppendLine("}\n}"); if (ns.Length > 0) sb.AppendLine("}");
        }
        return sb.ToString();
    }
    static string Designer(XDocument document, string className, bool vb, string path, bool original, bool hasBase, bool hasConstructor = true)
    {
        var root = document.Root!; var split = className.LastIndexOf('.'); var ns = split < 0 ? "" : className[..split]; var name = split < 0 ? className : className[(split + 1)..];
        var prefix = original ? "System.Windows" : Capabilities.Prefix;
        string Type(string n) => (vb ? "Global." : "global::") + prefix + (n == "Window" ? ".Window" : ".Controls." + n);
        string Quote(string text) => vb ? "\"" + text.Replace("\"", "\"\"") + "\"" : JsonSerializer.Serialize(text);
        var nodes = root.DescendantsAndSelf().Where(n => Capabilities.Controls.Contains(n.Name.LocalName) && !n.Ancestors().Any(p => p.Name.LocalName == "DataTemplate")).Select(n => (Node: n, Name: (string?)n.Attribute(X + "Name") ?? (string?)n.Attribute("Name"))).ToList();
        var usedNames = nodes.Where(n => n.Name != null).Select(n => n.Name!).ToHashSet(StringComparer.Ordinal);
        var autoNames = new Dictionary<XElement, string>(); int autoIndex = 0;
        foreach (var node in nodes.Where(n => n.Name == null)) { while (usedNames.Contains("__pz" + autoIndex)) autoIndex++; autoNames[node.Node] = "__pz" + autoIndex++; }
        var sb = new StringBuilder();
        if (vb)
        {
            if (ns.Length > 0) sb.AppendLine("Namespace " + ns);
            sb.AppendLine($"Partial Public Class [{name}]");
            if (!hasBase) sb.AppendLine("Inherits " + Type(root.Name.LocalName));
            foreach (var n in nodes.Where(n => n.Name != null)) sb.AppendLine($"Friend WithEvents [{n.Name}] As {Type(n.Node.Name.LocalName)}");
            if (!original && !hasConstructor) sb.AppendLine("Public Sub New()\nInitializeComponent()\nEnd Sub");
            if (!original) sb.AppendLine("Private _projectZInitialized As Boolean");
            sb.AppendLine("Private Sub InitializeComponent()");
            if (!original)
            {
                sb.AppendLine("If _projectZInitialized Then Return\n_projectZInitialized = True");
                sb.AppendLine($"Global.{Capabilities.Prefix}.XamlLoader.Initialize(Me, {Quote(path)})");
                foreach (var n in nodes.Where(n => n.Name != null)) sb.AppendLine($"Me.[{n.Name}] = DirectCast(Me.FindName({Quote(n.Name!)}), {Type(n.Node.Name.LocalName)})");
                foreach (var n in nodes) foreach (var a in n.Node.Attributes().Where(a => Capabilities.Events.Contains(a.Name.LocalName))) sb.AppendLine($"AddHandler {(n.Node == root ? "Me" : n.Name != null ? "Me.[" + n.Name + "]" : "DirectCast(Me.FindName(" + Quote(autoNames[n.Node]) + "), " + Type(n.Node.Name.LocalName) + ")")}.{a.Name.LocalName}, AddressOf Me.[{a.Value}]");
            }
            sb.AppendLine("End Sub\nEnd Class"); if (ns.Length > 0) sb.AppendLine("End Namespace");
        }
        else
        {
            if (ns.Length > 0) sb.AppendLine("namespace " + ns + " {");
            sb.AppendLine($"public partial class @{name}" + (hasBase ? "" : " : " + Type(root.Name.LocalName)) + " {");
            foreach (var n in nodes.Where(n => n.Name != null)) sb.AppendLine($"internal {Type(n.Node.Name.LocalName)} @{n.Name} = null!;");
            if (!original && !hasConstructor) sb.AppendLine($"public @{name}() {{ InitializeComponent(); }}");
            if (!original) sb.AppendLine("private bool _projectZInitialized;");
            sb.AppendLine("private void InitializeComponent() {");
            if (!original)
            {
                sb.AppendLine("if (_projectZInitialized) return; _projectZInitialized = true;");
                sb.AppendLine($"global::{Capabilities.Prefix}.XamlLoader.Initialize(this, {Quote(path)});");
                foreach (var n in nodes.Where(n => n.Name != null)) sb.AppendLine($"this.@{n.Name} = ({Type(n.Node.Name.LocalName)})FindName({Quote(n.Name!)})!;");
                foreach (var n in nodes) foreach (var a in n.Node.Attributes().Where(a => Capabilities.Events.Contains(a.Name.LocalName))) sb.AppendLine($"{(n.Node == root ? "this" : n.Name != null ? "this.@" + n.Name : "((" + Type(n.Node.Name.LocalName) + ")FindName(" + Quote(autoNames[n.Node]) + ")!)")}.{a.Name.LocalName} += this.@{a.Value};");
            }
            sb.AppendLine("}\n}"); if (ns.Length > 0) sb.AppendLine("}");
        }
        return sb.ToString();
    }
    void Add(string code, string file, SyntaxNode node, string message) { var line = node.GetLocation().GetLineSpan().StartLinePosition; issues.Add(new(code, file, line.Line + 1, line.Character + 1, message)); }
    static bool IsGenerated(string path) => path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(p => p is "obj" or "bin") || path.EndsWith(".g.cs") || path.EndsWith(".g.vb");
    static string SourceRelative(string directory, string path) { var relative = Path.GetRelativePath(directory, path); return relative.StartsWith("..") ? Path.Combine("Linked", Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(path)))[..8], Path.GetFileName(path)) : relative; }
    string SafePath(string relative) { var path = Path.GetFullPath(Path.Combine(output, relative)); if (!path.StartsWith(output + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Output path escapes destination."); return path; }
    void Write(string relative, string text) => WriteBytes(relative, new UTF8Encoding(false).GetBytes(text));
    void WriteBytes(string relative, byte[] data)
    {
        var path = SafePath(relative); var hash = Hash(data);
        if (File.Exists(path) && Hash(File.ReadAllBytes(path)) != hash)
        {
            var current = Hash(File.ReadAllBytes(path));
            var own = owned.LastOrDefault(f => f.Path == relative)?.Hash;
            if (current != own && (!previous.TryGetValue(relative, out var old) || current != old)) throw new IOException($"Refusing to overwrite edited/unowned file: {path}. Move handwritten extensions into Extensions/.");
        }
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (!File.Exists(path) || Hash(File.ReadAllBytes(path)) != hash) File.WriteAllBytes(path, data);
        owned.Add(new(relative, hash));
    }
    static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    static async Task<(int Code, string Text)> Command(string executable, string[] args)
    {
        var info = new ProcessStartInfo(executable) { RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        foreach (var key in info.Environment.Keys.Where(k => k.StartsWith("MSBUILD", StringComparison.OrdinalIgnoreCase)).ToArray()) info.Environment.Remove(key);
        foreach (var arg in args) info.ArgumentList.Add(arg);
        using var process = Process.Start(info)!; var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync(); await process.WaitForExitAsync(); return (process.ExitCode, await stdout + await stderr);
    }
}
