using System.Text.Json;
using System.Xml.Linq;
using ProjectZ.WpfCompatibility;

internal static class MarkupAudit
{
    public static int Run(string input, string output)
    {
        var files = Directory.Exists(input) ? Directory.EnumerateFiles(input, "*.xaml", SearchOption.AllDirectories).Where(n => !n.Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj")).OrderBy(n => n).ToArray() : new[] { input };
        var issues = new List<object>(); var empty = new List<string>(); int typeIssues = 0;
        foreach (var file in files)
        {
            var document = XDocument.Load(file, LoadOptions.SetLineInfo);
            foreach (var issue in XamlSupport.Validate(document))
            {
                bool missingType = issue.Message.StartsWith("Unsupported XAML element ", StringComparison.Ordinal) || issue.Message.StartsWith("Unsupported XAML namespace/type ", StringComparison.Ordinal);
                if (missingType) typeIssues++;
                issues.Add(new { File = file, issue.Line, issue.Column, issue.Message, MissingType = missingType });
            }
            empty.AddRange(MarkupTranslation.Translate(document).Select(n => file + ": " + n));
        }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        File.WriteAllText(output, JsonSerializer.Serialize(new { Files = files.Length, Diagnostics = issues.Count, MissingTypes = typeIssues, EmptyControls = empty, Issues = issues }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"XAML_AUDIT files={files.Length} diagnostics={issues.Count} missingTypes={typeIssues} emptyControls={empty.Count}");
        return issues.Count == 0 ? 0 : 1;
    }
}
