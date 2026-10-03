using System.Xml.Linq;
using System.Text.Json;

internal static class BuildViewImporter
{
    public static int Run(string manifestDirectory)
    {
        try
        {
            string directory = Path.GetFullPath(manifestDirectory);
            string Read(string name) => File.ReadAllText(Path.Combine(directory, name)).Trim();
            string[] Lines(string name) => File.ReadAllLines(Path.Combine(directory, name)).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            bool vb = Read("language") == "VB";
            var sources = Lines("sources").Where(p => !p.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(s => s.Equals("obj", StringComparison.OrdinalIgnoreCase) || s.Equals("bin", StringComparison.OrdinalIgnoreCase)) && !p.EndsWith(".g.cs") && !p.EndsWith(".g.vb")).ToArray();
            var props = new XElement("PropertyGroup", new XElement("TargetFramework", Read("framework")), new XElement("UseWPF", "true"),
                new XElement("EnableDefaultCompileItems", "false"), new XElement("EnableDefaultPageItems", "false"), new XElement("EnableDefaultApplicationDefinition", "false"),
                new XElement("RootNamespace", Read("namespace")), new XElement("ImplicitUsings", Read("implicit-usings")), new XElement("DefineConstants", string.Join(vb ? "," : ";", Lines("constants"))));
            var items = new XElement("ItemGroup");
            foreach (var source in sources) items.Add(new XElement("Compile", new XAttribute("Include", source)));
            foreach (var page in Lines("pages"))
            {
                var xaml = XDocument.Load(page);
                if (xaml.Root?.Name.LocalName == "Application") throw new InvalidOperationException("Build-time view conversion does not replace App.xaml startup. Use the project importer for application conversion.");
                items.Add(new XElement("Page", new XAttribute("Include", page)));
            }
            foreach (var reference in Lines("references"))
                items.Add(new XElement("Reference", new XAttribute("Include", Path.GetFileNameWithoutExtension(reference)), new XElement("HintPath", reference)));
            string input = Path.Combine(directory, "Input", "Views" + (vb ? ".vbproj" : ".csproj"));
            Directory.CreateDirectory(Path.GetDirectoryName(input)!);
            string xml = new XElement("Project", new XAttribute("Sdk", "Microsoft.NET.Sdk"), props, items).ToString();
            if (!File.Exists(input) || File.ReadAllText(input) != xml) File.WriteAllText(input, xml);
            string generated = Path.Combine(directory, "Generated");
            var selected = new HashSet<string>(Lines("convert-sources"), StringComparer.OrdinalIgnoreCase);
            foreach (var page in Lines("pages"))
                foreach (var candidate in new[] { page + (vb ? ".vb" : ".cs"), Path.ChangeExtension(page, vb ? ".Designer.vb" : ".Designer.cs"), page + (vb ? ".Designer.vb" : ".Designer.cs") })
                    if (sources.Contains(candidate, StringComparer.OrdinalIgnoreCase)) selected.Add(candidate);
            if (selected.Any(s => !sources.Contains(s, StringComparer.OrdinalIgnoreCase))) throw new InvalidOperationException("Every ProjectZXamlSource must also be a Compile item.");
            int result = ProjectImporter.GenerateForBuild(input, generated, selected);
            if (result != 0) return result;
            using var report = JsonDocument.Parse(File.ReadAllText(Path.Combine(generated, "projectz-import.report.json")));
            string project = report.RootElement.GetProperty("RootProject").GetString()!;
            string root = Path.GetDirectoryName(project)!;
            var selectedOutput = selected.Select(s => Path.GetFullPath(Path.Combine(root, "Source", ProjectImporter.SourceRelative(Path.GetDirectoryName(input)!, s)))).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var converted = XDocument.Load(project).Descendants("Compile").Select(e => (string)e.Attribute("Include")!).Where(s => !s.Contains('*')).Select(s => Path.GetFullPath(Path.Combine(root, s))).Where(s => selectedOutput.Contains(s) || s.StartsWith(Path.Combine(root, "Generated") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
            File.WriteAllLines(Path.Combine(directory, "compile-items"), converted);
            File.WriteAllLines(Path.Combine(directory, "original-items"), selected);
            File.WriteAllLines(Path.Combine(directory, "generated-items"), converted.Concat(Directory.GetFiles(generated, "*.xaml", SearchOption.AllDirectories)));
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine("Project-Z XAML build error: " + e.GetBaseException().Message); return 1; }
    }
}
