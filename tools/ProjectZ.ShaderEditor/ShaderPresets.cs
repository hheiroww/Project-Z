namespace ProjectZ.ShaderEditor;

/// <summary>An editable starting graph for a Project-Z pixel effect.</summary>
public sealed record ShaderPreset(string Id, string Name, string Category, string Description, Func<ShaderDocument> CreateDocument);

public static partial class ShaderPresets
{
    public static IReadOnlyList<ShaderPreset> All { get; } = Array.AsReadOnly(
        Materials().Concat(Particles()).Concat(ImageEffects())
            .OrderBy(preset => preset.Category, StringComparer.Ordinal)
            .ThenBy(preset => preset.Name, StringComparer.Ordinal)
            .ToArray());

    private static ShaderPreset Recipe(string id, string name, string category, string description, string expression,
        string colorNodeName, double[] rgba, string amountNodeName, double amount, string motionNodeName, double motion)
    {
        return new ShaderPreset(id, name, category, description, () =>
        {
            var source = ShaderNodes.Create("Source", 32, 28);
            var color = ShaderNodes.Create("Color", 32, 150);
            color.Name = colorNodeName;
            color.Values = (double[])rgba.Clone();
            var amountNode = ShaderNodes.Create("Float", 32, 274);
            amountNode.Name = amountNodeName;
            amountNode.Values[0] = amount;
            var motionNode = ShaderNodes.Create("Float", 32, 396);
            motionNode.Name = motionNodeName;
            motionNode.Values[0] = motion;
            var controls = ShaderNodes.Create("Combine4", 235, 284);
            controls.Name = "Effect controls";
            var effect = ShaderNodes.Create("CustomFloat4", 444, 104);
            effect.Name = name;
            effect.Expression = expression;
            var output = ShaderNodes.Create("Output", 658, 104);
            return new ShaderDocument
            {
                Name = name,
                Mode = ShaderMode.Advanced,
                Nodes = [source, color, amountNode, motionNode, controls, effect, output],
                Connections = [
                    Connect(amountNode, controls, "R"), Connect(motionNode, controls, "G"),
                    Connect(source, effect, "A"), Connect(color, effect, "B"), Connect(controls, effect, "C"),
                    Connect(effect, output, "Color")
                ]
            };
        });
    }

    private static ShaderConnection Connect(ShaderNode source, ShaderNode target, string input) =>
        new() { Source = source.Id, Target = target.Id, Input = input };
}
