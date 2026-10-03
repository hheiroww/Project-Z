using System.Xml.Linq;

// A linked source project lets the regular semantic importer do the conversion.
internal static class ViewImporter
{
    public static int Run(string[] args)
    {
        try
        {
            if (args.Length == 0) throw new ArgumentException("Usage: import-view <view.xaml> --output <directory> [--code-behind <file>] [--designer <file>] [--source <file>] [--root-namespace <VB namespace>]");
            string xaml = Existing(args[0]), output = "", code = "", rootNamespace = "";
            var sources = new List<string>();
            bool designerProvided = false;
            for (int i = 1; i < args.Length; i += 2)
            {
                if (i + 1 >= args.Length) throw new ArgumentException("Missing value for " + args[i]);
                switch (args[i])
                {
                    case "--output": output = Path.GetFullPath(args[i + 1]); break;
                    case "--code-behind": code = Existing(args[i + 1]); break;
                    case "--root-namespace": rootNamespace = args[i + 1]; break;
                    case "--designer": designerProvided = true; sources.Add(Existing(args[i + 1])); break;
                    case "--source": sources.Add(Existing(args[i + 1])); break;
                    default: throw new ArgumentException("Unknown option: " + args[i]);
                }
            }
            if (output.Length == 0) throw new ArgumentException("Specify --output with a separate destination directory.");
            if (code.Length == 0)
            {
                var matches = new[] { xaml + ".cs", xaml + ".vb" }.Where(File.Exists).ToArray();
                if (matches.Length != 1) throw new ArgumentException("Specify --code-behind: expected exactly one matching .xaml.cs or .xaml.vb file.");
                code = matches[0];
            }
            bool vb = code.EndsWith(".vb", StringComparison.OrdinalIgnoreCase);
            string extension = vb ? ".vb" : ".cs";
            if (!code.EndsWith(extension, StringComparison.OrdinalIgnoreCase) || sources.Any(s => !s.EndsWith(extension, StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("Code-behind and supporting files must all use C# or all use VB.");
            if (!designerProvided)
            {
                foreach (string candidate in new[] { Path.ChangeExtension(xaml, ".Designer" + extension), xaml + ".Designer" + extension })
                    if (File.Exists(candidate)) sources.Add(candidate);
            }
            sources.Insert(0, code);
            if (sources.Append(xaml).Any(s => s.StartsWith(output.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("The output directory must not contain the source files.");
            var props = new XElement("PropertyGroup", new XElement("TargetFramework", "net8.0-windows"), new XElement("UseWPF", "true"),
                new XElement("RootNamespace", rootNamespace), new XElement("EnableDefaultCompileItems", "false"), new XElement("EnableDefaultPageItems", "false"),
                new XElement("EnableDefaultApplicationDefinition", "false"));
            var items = new XElement("ItemGroup", new XElement("Page", new XAttribute("Include", xaml), new XElement("Link", Path.GetFileName(xaml))));
            foreach (var file in sources.Distinct(StringComparer.OrdinalIgnoreCase))
                items.Add(new XElement("Compile", new XAttribute("Include", file), new XElement("Link", Path.GetFileName(file))));
            string identity = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(xaml + extension)))[..12];
            string input = Path.Combine(output, ".projectz-input", "LinkedView_" + identity + (vb ? ".vbproj" : ".csproj"));
            string xml = new XDocument(new XComment(" Project-Z generated linked-view input; edit the original files instead. "), new XElement("Project", new XAttribute("Sdk", "Microsoft.NET.Sdk"), props, items)).ToString();
            if (File.Exists(input) && File.ReadAllText(input) != xml)
                throw new IOException("Linked input already exists with different contents. Use a new output directory.");
            Directory.CreateDirectory(Path.GetDirectoryName(input)!);
            File.WriteAllText(input, xml);
            return ProjectImporter.Run(new[] { input, "--output", Path.Combine(output, "Converted") });
        }
        catch (Exception e) { Console.Error.WriteLine(e.GetBaseException().Message); return 1; }
    }
    static string Existing(string value)
    {
        string path = Path.GetFullPath(value);
        return File.Exists(path) ? path : throw new FileNotFoundException("Source file not found.", path);
    }
}
