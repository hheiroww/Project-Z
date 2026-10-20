using System.Xml.Linq;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;

internal static class NativeObjectExporter
{
    internal static int Run(string project, string destination)
    {
        if (!MSBuildLocator.IsRegistered) MSBuildLocator.RegisterDefaults();
        return ExportAsync(project, destination).GetAwaiter().GetResult();
    }
    static async Task<int> ExportAsync(string path, string destination)
    {
        using var workspace = MSBuildWorkspace.Create(new Dictionary<string, string> { ["Configuration"] = "Release" });
        workspace.LoadMetadataForReferencedProjects = true;
        var project = await workspace.OpenProjectAsync(path);
        Directory.CreateDirectory(destination);
        int count = 0;
        foreach (var document in project.Documents)
        {
            if (document.FilePath == null || document.FilePath.Split(Path.DirectorySeparatorChar).Any(p => p is "obj" or "bin")) continue;
            var syntax = await document.GetSyntaxRootAsync();
            var model = await document.GetSemanticModelAsync();
            if (syntax == null || model == null) continue;
            var rewritten = new ObjectRewriter(model, syntax);
            var result = rewritten.Visit(syntax)!;
            if (rewritten.Markup.Count == 0) continue;
            File.WriteAllText(document.FilePath, result.ToFullString());
            foreach (var (className, markup) in rewritten.Markup)
            {
                new XDocument(markup).Save(Path.Combine(destination, Path.GetFileNameWithoutExtension(document.FilePath) + "." + className.Split('.').Last() + ".Objects.xaml"));
                count += markup.Elements().Count(e => e.Name.LocalName == "ObjectFactory");
            }
        }
        Console.WriteLine($"Project-Z object export: {count} native and desktop control templates.");
        return 0;
    }
    sealed class ObjectRewriter(SemanticModel model, SyntaxNode source) : CSharpSyntaxRewriter
    {
        internal Dictionary<string, XElement> Markup = new();
        static readonly XNamespace Ns = "urn:projectz:native-scene";
        int index;
        static bool IsControl(INamedTypeSymbol? type)
        {
            for (var current = type; current != null; current = current.BaseType)
                if (current.ToDisplayString() is "ProjectZ.Shared.Drawing.UI.SceneElement" or "System.Windows.Forms.Control") return true;
            return false;
        }
        public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            var visited = (ClassDeclarationSyntax)base.VisitClassDeclaration(node)!;
            var type = model.GetDeclaredSymbol(node)!;
            if (Markup.ContainsKey(type.ToDisplayString()) && !visited.Modifiers.Any(SyntaxKind.PartialKeyword))
                visited = visited.AddModifiers(SyntaxFactory.Token(SyntaxKind.PartialKeyword).WithTrailingTrivia(SyntaxFactory.Space));
            return visited;
        }
        public override SyntaxNode? VisitObjectCreationExpression(ObjectCreationExpressionSyntax node) => Rewrite(node, node.ArgumentList, node.Initializer) ?? base.VisitObjectCreationExpression(node);
        public override SyntaxNode? VisitImplicitObjectCreationExpression(ImplicitObjectCreationExpressionSyntax node) => Rewrite(node, node.ArgumentList, node.Initializer) ?? base.VisitImplicitObjectCreationExpression(node);
        SyntaxNode? Rewrite(ExpressionSyntax node, ArgumentListSyntax? arguments, InitializerExpressionSyntax? initializer)
        {
            var type = model.GetTypeInfo(node).Type as INamedTypeSymbol;
            if (!IsControl(type) || initializer == null) return null;
            var owner = node.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
            if (owner == null || owner.Ancestors().OfType<ClassDeclarationSyntax>().Any()) return null;
            var className = model.GetDeclaredSymbol(owner)!.ToDisplayString();
            if (!Markup.TryGetValue(className, out var root))
            {
                root = new XElement(Ns + "SceneTemplates", new XAttribute("Class", className), new XAttribute(XNamespace.Xmlns + "x", NativeSceneCompiler.X));
                foreach (var use in source.DescendantNodes().OfType<UsingDirectiveSyntax>().Where(u => !u.GlobalKeyword.IsKind(SyntaxKind.GlobalKeyword))) root.Add(new XElement(Ns + "Using", use.ToString()[6..].Trim().TrimEnd(';')));
                Markup.Add(className, root);
            }
            var name = "UiObject_" + Path.GetFileNameWithoutExtension(source.SyntaxTree.FilePath).Replace('.', '_') + "_" + (++index).ToString("D3");
            var factory = new XElement(Ns + "ObjectFactory", new XAttribute(NativeSceneCompiler.X + "Name", name), new XAttribute("Type", type!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
            if (node.Ancestors().OfType<MethodDeclarationSyntax>().Any(m => m.Modifiers.Any(SyntaxKind.StaticKeyword)) || node.Ancestors().OfType<FieldDeclarationSyntax>().Any()) factory.SetAttributeValue("Static", "true");
            var call = new List<ArgumentSyntax>();
            string Value(ExpressionSyntax expression, out string typeName)
            {
                var resolved = model.GetTypeInfo(expression).ConvertedType ?? model.GetTypeInfo(expression).Type ?? throw new InvalidDataException($"Cannot resolve {expression}.");
                typeName = resolved.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                if (model.GetConstantValue(expression).HasValue || expression is ThisExpressionSyntax)
                    return "{Expression " + expression.WithoutTrivia() + "}";
                ArgumentListSyntax? creationArguments = expression switch
                {
                    ImplicitObjectCreationExpressionSyntax implicitCreation when implicitCreation.Initializer == null => implicitCreation.ArgumentList,
                    ObjectCreationExpressionSyntax explicitCreation when explicitCreation.Initializer == null => explicitCreation.ArgumentList,
                    _ => null
                };
                if (creationArguments != null && creationArguments.Arguments.All(a => a.NameColon == null && a.RefKindKeyword == default))
                {
                    var values = creationArguments.Arguments.Select(a => { var value = Value(a.Expression, out var argumentType); return NativeSceneCompiler.Expression(value, argumentType); }).ToArray();
                    return "{Expression new " + typeName + "(" + string.Join(", ", values) + ")}";
                }
                var parameter = "p" + call.Count;
                factory.Add(new XElement(Ns + "Parameter", new XAttribute("Name", parameter), new XAttribute("Type", "System.Func<" + typeName + ">")));
                var lambda = SyntaxFactory.ParenthesizedLambdaExpression(expression.WithoutTrivia());
                call.Add(SyntaxFactory.Argument(lambda));
                return "{Expression " + parameter + "()}";
            }
            foreach (var argument in arguments?.Arguments ?? default)
            {
                if (argument.NameColon != null || argument.RefKindKeyword != default) throw new InvalidDataException("Named/ref constructor arguments are not supported by native object extraction.");
                var value = Value(argument.Expression, out var argumentType);
                factory.Add(new XElement(Ns + "Argument", new XAttribute("Type", argumentType), new XAttribute("Value", value)));
            }
            foreach (var entry in initializer.Expressions)
            {
                if (entry is not AssignmentExpressionSyntax assignment || assignment.Left is not IdentifierNameSyntax property) throw new InvalidDataException("Native object extraction requires property initializers.");
                var value = Value(assignment.Right, out var propertyType);
                factory.Add(new XElement(Ns + "Set", new XAttribute("Property", property.Identifier.ValueText), new XAttribute("Type", propertyType), new XAttribute("Value", value)));
            }
            root.Add(factory);
            return SyntaxFactory.InvocationExpression(SyntaxFactory.IdentifierName(name), SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(call))).WithTriviaFrom(node);
        }
    }
}
