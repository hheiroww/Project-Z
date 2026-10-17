using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

// One-time native scene extraction. Subsequent edits/builds use only XAML + MSBuild.
internal static class NativeSceneExporter
{
    public static int Run(string source, string definitions)
    {
        var doc = XDocument.Load(definitions);
        var root = doc.Root!;
        var factories = root.Elements().Where(e => e.Name.LocalName == "Factory").ToDictionary(NativeSceneCompiler.Name);
        var count = 0;
        foreach (var path in Directory.GetFiles(source, "*.cs", SearchOption.TopDirectoryOnly).OrderBy(p => p))
        {
            var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(path));
            var syntax = tree.GetRoot();
            // Export only members belonging to the declared owning scene class.
            var owner = syntax.DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault(c => c.Identifier.ValueText == NativeSceneCompiler.Required(root, "Class").Split('.').Last());
            if (owner == null) continue;
            var output = new XElement(root.Name, new XAttribute("Class", NativeSceneCompiler.Required(root, "Class")), new XAttribute(XNamespace.Xmlns + "x", NativeSceneCompiler.X));
            var rewriter = new Rewriter(factories, output, Path.GetFileNameWithoutExtension(path).Replace('.', '_'));
            var updated = rewriter.Visit(syntax)!;
            if (!output.Elements().Any()) continue;
            File.WriteAllText(path, updated.ToFullString());
            var destination = Path.Combine(Path.GetDirectoryName(definitions)!, Path.GetFileNameWithoutExtension(path) + ".xaml");
            new XDocument(output).Save(destination);
            count += output.Elements().Count();
        }
        Console.WriteLine($"Project-Z native scene export: {count} layout templates.");
        return 0;
    }

    sealed class Rewriter(Dictionary<string, XElement> factories, XElement output, string prefix) : CSharpSyntaxRewriter
    {
        int sequence;
        public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            if (factories.TryGetValue(node.Identifier.ValueText, out var factory) && (string?)factory.Attribute("Replace") == "true") return null;
            return base.VisitMethodDeclaration(node);
        }
        static readonly Dictionary<string, string> VisualTypes = new()
        {
            ["Font"] = "string", ["Size"] = "Vector2", ["Position"] = "Vector2", ["TextPadding"] = "Vector2",
            ["CornerRadius"] = "float", ["BorderThickness"] = "float", ["MaxWidth"] = "float", ["MaxHeight"] = "float",
            ["BackgroundColor"] = "Color", ["ForegroundColor"] = "Color", ["BorderColor"] = "Color",
            ["MouseOverBackgroundColor"] = "Color", ["MouseDownBackgroundColor"] = "Color",
            ["Clip"] = "bool", ["AutoSizeWidthOnTextOverflow"] = "bool", ["IsOverlay"] = "bool",
            ["TextWrapping"] = "TextWrapping", ["TextTrimming"] = "TextTrimming", ["Stretch"] = "ImageStretch"
        };
        static bool Constant(ExpressionSyntax value) => value is LiteralExpressionSyntax ||
            value is PrefixUnaryExpressionSyntax unary && unary.Operand is LiteralExpressionSyntax ||
            value is ImplicitObjectCreationExpressionSyntax implicitNew && implicitNew.ArgumentList.Arguments.All(a => Constant(a.Expression)) ||
            value is ObjectCreationExpressionSyntax create && create.ArgumentList != null && create.ArgumentList.Arguments.All(a => Constant(a.Expression)) ||
            value is MemberAccessExpressionSyntax member && member.Expression is IdentifierNameSyntax type && type.Identifier.ValueText is "Color" or "TextWrapping" or "TextTrimming" or "ImageStretch";
        static string MarkupValue(ExpressionSyntax value, string type)
        {
            if (value is LiteralExpressionSyntax literal) return type == "string" ? literal.Token.ValueText : literal.Token.Value is bool boolean ? (boolean ? "true" : "false") : Convert.ToString(literal.Token.Value, System.Globalization.CultureInfo.InvariantCulture)!;
            if (type == "Vector2" && value is ImplicitObjectCreationExpressionSyntax vector && vector.ArgumentList.Arguments.All(a => a.Expression is LiteralExpressionSyntax))
                return string.Join(",", vector.ArgumentList.Arguments.Select(a => Convert.ToString(((LiteralExpressionSyntax)a.Expression).Token.Value, System.Globalization.CultureInfo.InvariantCulture)));
            if (type == "Color" && value is ObjectCreationExpressionSyntax color && color.Type.ToString() == "Color" && color.ArgumentList is {} args && args.Arguments.Count is 3 or 4 && args.Arguments.All(a => a.Expression is LiteralExpressionSyntax l && l.Token.Value is int n && n is >= 0 and <= 255))
            {
                var bytes = args.Arguments.Select(a => (int)((LiteralExpressionSyntax)a.Expression).Token.Value!).ToArray();
                return "#" + (bytes.Length == 4 ? bytes[3].ToString("X2") : "") + string.Concat(bytes.Take(3).Select(b => b.ToString("X2")));
            }
            return "{Expression " + value.WithoutTrivia() + "}";
        }
        public override SyntaxNode? VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
        {
            if (node.Type.ToString() != "Color" || node.ArgumentList == null || !node.ArgumentList.Arguments.All(a => Constant(a.Expression))) return base.VisitObjectCreationExpression(node);
            var id = "Color_" + prefix + "_" + (++sequence).ToString("D3");
            output.Add(new XElement(output.Name.Namespace + "Resource", new XAttribute(NativeSceneCompiler.X + "Name", id), new XAttribute("Type", "Color"), new XAttribute("Value", MarkupValue(node, "Color"))));
            return SyntaxFactory.IdentifierName(id).WithTriviaFrom(node);
        }
        public override SyntaxNode? VisitAssignmentExpression(AssignmentExpressionSyntax node)
        {
            var visited = (AssignmentExpressionSyntax)base.VisitAssignmentExpression(node)!;
            if (node.Left is not MemberAccessExpressionSyntax property || !VisualTypes.TryGetValue(property.Name.Identifier.ValueText, out var type) || !Constant(node.Right)) return visited;
            if (type == "Vector2" && node.Right is not ImplicitObjectCreationExpressionSyntax && node.Right is not ObjectCreationExpressionSyntax) return visited;
            if (node.Ancestors().OfType<MethodDeclarationSyntax>().Any(m => factories.TryGetValue(m.Identifier.ValueText, out var own) && (string?)own.Attribute("External") != "true")) return visited;
            var id = "Style_" + prefix + "_" + (++sequence).ToString("D3") + "_" + property.Name.Identifier.ValueText;
            var text = MarkupValue(node.Right, type);
            output.Add(new XElement(output.Name.Namespace + "Resource", new XAttribute(NativeSceneCompiler.X + "Name", id), new XAttribute("Type", type), new XAttribute("Value", text)));
            return visited.WithRight(SyntaxFactory.IdentifierName(id).WithTriviaFrom(visited.Right));
        }
        public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            // Rewrite children first: an event lambda can itself construct UI.
            var visited = (InvocationExpressionSyntax)base.VisitInvocationExpression(node)!;
            var name = visited.Expression switch { IdentifierNameSyntax i => i.Identifier.ValueText, MemberAccessExpressionSyntax m => m.Name.Identifier.ValueText, _ => null };
            if (name == null || !factories.TryGetValue(name, out var factory)) return visited;
            if (node.Ancestors().OfType<MethodDeclarationSyntax>().Any(m => factories.TryGetValue(m.Identifier.ValueText, out var own) && (string?)own.Attribute("External") != "true")) return visited;
            var parameters = NativeSceneCompiler.Parameters(factory);
            var id = "Ui_" + prefix + "_" + (++sequence).ToString("D3");
            var element = new XElement(output.Name.Namespace + ((string?)factory.Attribute("Tag") ?? name), new XAttribute(NativeSceneCompiler.X + "Name", id), new XAttribute("Factory", name));
            var callArgs = new List<ArgumentSyntax>();
            int position = 0;
            foreach (var argument in visited.ArgumentList.Arguments)
            {
                var p = argument.NameColon is {} named ? parameters.Single(p => NativeSceneCompiler.Required(p, "Name") == named.Name.Identifier.ValueText) : parameters[position];
                position++;
                var paramName = NativeSceneCompiler.Required(p, "Name");
                var attr = (string?)p.Attribute("Attribute") ?? paramName;
                var type = NativeSceneCompiler.Required(p, "Type");
                var value = argument.Expression;
                if (value is LiteralExpressionSyntax literal)
                {
                    var text = type == "string" && literal.IsKind(SyntaxKind.StringLiteralExpression) ? literal.Token.ValueText
                        : literal.IsKind(SyntaxKind.NumericLiteralExpression) ? Convert.ToString(literal.Token.Value, System.Globalization.CultureInfo.InvariantCulture)! : literal.ToString();
                    if (text == "null") text = "{Expression null}";
                    element.SetAttributeValue(attr, text);
                }
                else if (value is IdentifierNameSyntax resource && (string?)p.Attribute("Resource") == "true") element.SetAttributeValue(attr, "{Resource " + resource.Identifier.ValueText + "}");
                else if (value is ObjectCreationExpressionSyntax creation && creation.Type.ToString() == "Color" && creation.ArgumentList!.Arguments.All(a => a.Expression is LiteralExpressionSyntax))
                    element.SetAttributeValue(attr, "{Expression " + value.WithoutTrivia() + "}");
                else
                {
                    element.SetAttributeValue(attr, "{Parameter " + paramName + "}");
                    element.Add(new XElement(output.Name.Namespace + "Parameter", new XAttribute("Name", paramName), new XAttribute("Type", type)));
                    callArgs.Add(argument.WithNameColon(null));
                }
            }
            output.Add(element);
            ExpressionSyntax expression = visited.Expression switch
            {
                MemberAccessExpressionSyntax member => member.WithName(SyntaxFactory.IdentifierName(id)),
                _ => SyntaxFactory.IdentifierName(id).WithTriviaFrom(visited.Expression)
            };
            return visited.WithExpression(expression).WithArgumentList(visited.ArgumentList.WithArguments(SyntaxFactory.SeparatedList(callArgs)));
        }
    }
}
