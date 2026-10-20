using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

// Fold constant post-construction appearance into the owning XAML element so
// editing Width/Height/Font has one authoritative location.
internal static class NativeStyleConsolidator
{
    internal static int Run(string directory, string markupDirectory)
    {
        var docs = Directory.GetFiles(markupDirectory, "*.xaml").ToDictionary(p => p, p => XDocument.Load(p));
        var nodes = docs.Values.SelectMany(d => d.Root!.Elements()).Where(e => e.Attribute(NativeSceneCompiler.X + "Name") != null).ToArray();
        var templates = nodes.Where(e => e.Attribute("Factory") != null).ToDictionary(NativeSceneCompiler.Name);
        var resources = nodes.Where(e => e.Name.LocalName == "Resource").ToDictionary(NativeSceneCompiler.Name);
        var moved = new HashSet<string>();
        int changes = 0;
        foreach (var source in Directory.GetFiles(directory, "*.cs"))
        {
            var syntax = CSharpSyntaxTree.ParseText(File.ReadAllText(source)).GetRoot();
            var rewriter = new Rewriter(syntax, templates, resources, moved);
            var result = rewriter.Visit(syntax)!;
            if (rewriter.Count > 0) File.WriteAllText(source, result.ToFullString());
            changes += rewriter.Count;
        }
        var sources = string.Join("\n", Directory.GetFiles(directory, "*.cs").Select(File.ReadAllText));
        foreach (var id in moved)
        {
            if (Regex.IsMatch(sources, @"\b" + Regex.Escape(id) + @"\b")) continue;
            if (docs.Values.SelectMany(d => d.Descendants().Attributes()).Any(a => a.Name != NativeSceneCompiler.X + "Name" && a.Value.Contains(id, StringComparison.Ordinal))) continue;
            resources[id].Remove();
        }
        foreach (var (path, doc) in docs) doc.Save(path);
        Console.WriteLine($"Project-Z native XAML: consolidated {changes} appearance assignments.");
        return 0;
    }
    sealed class Rewriter : CSharpSyntaxRewriter
    {
        readonly Dictionary<string, XElement> templates, resources;
        readonly HashSet<string> moved;
        readonly List<(string Owner, int Scope, int Position, string Template)> creations = [];
        internal int Count;
        static string? Method(InvocationExpressionSyntax call) => call.Expression switch { IdentifierNameSyntax i => i.Identifier.ValueText, MemberAccessExpressionSyntax m => m.Name.Identifier.ValueText, _ => null };
        static int Scope(SyntaxNode node) => node.Ancestors().FirstOrDefault(n => n is BaseMethodDeclarationSyntax or LocalFunctionStatementSyntax)?.SpanStart ?? -1;
        internal Rewriter(SyntaxNode syntax, Dictionary<string, XElement> templates, Dictionary<string, XElement> resources, HashSet<string> moved)
        {
            this.templates = templates; this.resources = resources; this.moved = moved;
            foreach (var call in syntax.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                var method = Method(call);
                if (method == null || !templates.ContainsKey(method)) continue;
                string? owner = call.Parent switch
                {
                    EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax variable } => variable.Identifier.ValueText,
                    AssignmentExpressionSyntax assignment when assignment.Right == call => assignment.Left.WithoutTrivia().ToString(),
                    _ => null
                };
                if (owner != null) creations.Add((owner, Scope(call), call.SpanStart, method));
            }
        }
        public override SyntaxNode? VisitExpressionStatement(ExpressionStatementSyntax node)
        {
            var targets = new List<(XElement Element, string Property)>();
            ExpressionSyntax expression = node.Expression;
            while (expression is AssignmentExpressionSyntax assignment && assignment.IsKind(SyntaxKind.SimpleAssignmentExpression))
            {
                if (assignment.Left is not MemberAccessExpressionSyntax property) return base.VisitExpressionStatement(node);
                string name = property.Name.Identifier.ValueText;
                if (name != "Size" && !NativeSceneCompiler.Overrides.ContainsKey(name)) return base.VisitExpressionStatement(node);
                var owner = property.Expression.WithoutTrivia().ToString();
                var creation = creations.LastOrDefault(c => c.Owner == owner && c.Scope == Scope(node) && c.Position < node.SpanStart);
                if (creation.Template == null) return base.VisitExpressionStatement(node);
                targets.Add((templates[creation.Template], name));
                expression = assignment.Right;
            }
            if (targets.Count == 0 || expression is not IdentifierNameSyntax resource || !resources.TryGetValue(resource.Identifier.ValueText, out var definition)) return base.VisitExpressionStatement(node);
            var value = NativeSceneCompiler.Required(definition, "Value");
            if (targets.Any(t => t.Property == "Size") && (value.StartsWith('{') || value.Split(',').Length != 2)) return base.VisitExpressionStatement(node);
            foreach (var (element, property) in targets)
            {
                if (property == "Size")
                {
                    var vector = value.Split(',');
                    element.SetAttributeValue("Width", vector[0]); element.SetAttributeValue("Height", vector[1]);
                }
                else element.SetAttributeValue(property, value);
            }
            moved.Add(resource.Identifier.ValueText); Count += targets.Count;
            return null;
        }
    }
}
