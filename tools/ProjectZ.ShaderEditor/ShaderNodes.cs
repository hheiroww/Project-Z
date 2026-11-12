using System.Diagnostics.CodeAnalysis;
using System.IO;
using static ProjectZ.ShaderEditor.ShaderValueType;

namespace ProjectZ.ShaderEditor;

public static class ShaderNodes
{
    public static IReadOnlyList<ShaderNodeDefinition> All { get; } = BuildCatalog().AsReadOnly();
    private static readonly Dictionary<string, ShaderNodeDefinition> ByKind = All.ToDictionary(node => node.Kind, StringComparer.Ordinal);

    public static bool TryGet(string kind, [NotNullWhen(true)] out ShaderNodeDefinition? definition)
    {
        definition = null;
        return kind is not null && ByKind.TryGetValue(kind, out definition);
    }

    public static ShaderNodeDefinition Get(string kind) =>
        TryGet(kind, out var definition) ? definition : throw new InvalidDataException($"Unknown shader node '{kind}'.");

    public static ShaderNode Create(string kind, double x, double y)
    {
        var definition = Get(kind);
        return new ShaderNode
        {
            Kind = kind,
            Expression = definition.DefaultCustomExpression ?? "",
            X = x,
            Y = y,
            Values = definition.Parameters.Select(parameter => parameter.DefaultValue).ToArray()
        };
    }

    public static bool CanConnect(ShaderDocument document, Guid source, Guid target, string input)
    {
        var sourceNode = document.Nodes.Find(node => node.Id == source);
        var targetNode = document.Nodes.Find(node => node.Id == target);
        if (sourceNode is null || targetNode is null || !PortsMatch(sourceNode, targetNode, input))
            return false;

        var upstream = document.Connections.ToLookup(wire => wire.Target);
        var seen = new HashSet<Guid>();
        var pending = new Stack<Guid>();
        pending.Push(source);
        while (pending.TryPop(out var current))
        {
            if (current == target)
                return false;
            if (seen.Add(current))
                foreach (var wire in upstream[current])
                    pending.Push(wire.Source);
        }
        return true;
    }

    internal static bool PortsMatch(ShaderNode source, ShaderNode target, string input)
    {
        var outputType = Get(source.Kind).Output;
        var inputPort = Get(target.Kind).Inputs.FirstOrDefault(port => port.Name == input);
        return outputType.HasValue && inputPort is not null && (outputType == inputPort.Type || outputType == Float);
    }

    private static List<ShaderNodeDefinition> BuildCatalog()
    {
        const string source = "SampleSource(uv)";
        const string black = "float4(0.0, 0.0, 0.0, 1.0)";
        const string white = "float4(1.0, 1.0, 1.0, 1.0)";
        var nodes = new List<ShaderNodeDefinition>();
        var uv = Port("UV", Float2, "uv");
        ShaderPort[] procedural = [uv, Port("A", Float4, black), Port("B", Float4, white)];
        ShaderParameter[] layerParameters = [new("Opacity", 1), new("Mode", 0), new("Enabled", 1)];

        void Add(string kind, string label, string category, ShaderValueType? type, ShaderPort[] inputs,
            Func<string[], string[], string> expression, params ShaderParameter[] parameters) =>
            nodes.Add(new ShaderNodeDefinition(kind, label, category, type, inputs, parameters, expression));

        Add("Output", "XAML Output", "Output", null, [Port("Color", Float4, source), Port("Alpha", Float, "")],
            (input, _) => input[0]);
        Add("Source", "Source Texture", "Inputs", Float4, [uv], (input, _) => $"SampleSource({input[0]})");
        Add("UV", "UV Coordinates", "Inputs", Float2, [], (_, _) => "uv");
        Add("PixelPosition", "Pixel Position", "Inputs", Float2, [], (_, _) => "input.Position.xy");
        Add("Time", "Time", "Inputs", Float, [], (_, _) => "Time");
        Add("TexelSize", "Texel Size", "Inputs", Float2, [], (_, _) => "TexelSize");
        Add("Float", "Float", "Inputs", Float, [], (_, values) => values[0], new ShaderParameter("Value", 1));
        Add("Color", "Color", "Inputs", Float4, [], (_, values) => $"float4({string.Join(", ", values)})",
            new ShaderParameter("R", 1), new ShaderParameter("G", 1), new ShaderParameter("B", 1), new ShaderParameter("A", 1));
        Add("Vector2", "Vector 2", "Inputs", Float2, [], (_, values) => $"float2({string.Join(", ", values)})",
            new ShaderParameter("X", 0), new ShaderParameter("Y", 0));
        Add("Vector3", "Vector 3", "Inputs", Float3, [], (_, values) => $"float3({string.Join(", ", values)})",
            new ShaderParameter("X", 0), new ShaderParameter("Y", 0), new ShaderParameter("Z", 0));
        Add("Vector4", "Vector 4", "Inputs", Float4, [], (_, values) => $"float4({string.Join(", ", values)})",
            new ShaderParameter("X", 0), new ShaderParameter("Y", 0), new ShaderParameter("Z", 0), new ShaderParameter("W", 0));

        Add("Gradient", "Gradient", "Patterns", Float4, procedural,
            (input, values) => $"lerp({input[1]}, {input[2]}, GradientFactor({input[0]}, {values[0]}))", new ShaderParameter("Angle", 0));
        Add("Checker", "Checker", "Patterns", Float4, procedural,
            (input, values) => $"lerp({input[1]}, {input[2]}, CheckerFactor({input[0]} * {values[0]}))", new ShaderParameter("Scale", 8));
        Add("Noise", "Noise", "Patterns", Float4, procedural,
            (input, values) => $"lerp({input[1]}, {input[2]}, ValueNoise({input[0]} * {values[0]}))", new ShaderParameter("Scale", 8));
        Add("RadialGradient", "Radial Gradient", "Patterns", Float4, procedural,
            (input, values) => $"lerp({input[1]}, {input[2]}, saturate(length({input[0]} - 0.5) * {values[0]}))", new ShaderParameter("Scale", 2));
        Add("Voronoi", "Voronoi", "Patterns", Float4, procedural,
            (input, values) => $"lerp({input[1]}, {input[2]}, VoronoiFactor({input[0]} * {values[0]}))", new ShaderParameter("Scale", 8));

        Add("LayerBlend", "Color Layer", "Layers", Float4, [Port("Base", Float4, source), Port("Layer", Float4, "0.0")],
            (input, values) => $"BlendLayer({input[0]}, {input[1]}, {values[0]} * step(0.5, {values[2]}), {values[1]})", layerParameters);
        Add("AlphaBlend", "Alpha Layer", "Layers", Float, [Port("Base", Float, source + ".a"), Port("Layer", Float, "1.0")],
            (input, values) => $"lerp({input[0]}, BlendRgb({input[0]}, {input[1]}, {values[1]}).r, saturate({values[0]}) * step(0.5, {values[2]}))",
            layerParameters);
        Add("SetAlpha", "Set Alpha", "Layers", Float4, [Port("Color", Float4, source), Port("Alpha", Float, "1.0")],
            (input, _) => $"float4(({input[0]}).rgb, {input[1]})");

        Add("TransformUV", "Transform UV", "Coordinates", Float2, [uv, Port("Scale", Float2, "float2(1.0, 1.0)"), Port("Offset", Float2, "float2(0.0, 0.0)"),
            Port("Rotation", Float, "0.0")], (input, _) => $"TransformUV({input[0]}, {input[1]}, {input[2]}, {input[3]})");
        Add("TileUV", "Tile UV", "Coordinates", Float2, [uv, Port("Count", Float2, "float2(2.0, 2.0)")],
            (input, _) => $"frac({input[0]} * {input[1]})");
        Add("PixelateUV", "Pixelate UV", "Coordinates", Float2, [uv, Port("Count", Float2, "float2(16.0, 16.0)")],
            (input, _) => $"(floor({input[0]} * max({input[1]}, 1.0)) + 0.5) / max({input[1]}, 1.0)");
        Add("PolarUV", "Polar UV", "Coordinates", Float2, [uv], (input, _) => $"PolarUV({input[0]})");
        Add("FlipUV", "Flip UV", "Coordinates", Float2, [uv],
            (input, values) => $"lerp({input[0]}, 1.0 - {input[0]}, float2({values[0]}, {values[1]}))",
            new ShaderParameter("Flip X", 1), new ShaderParameter("Flip Y", 0));

        foreach (var channel in new[] { ("Red", "r"), ("Green", "g"), ("Blue", "b"), ("Alpha", "a") })
            Add(channel.Item1, channel.Item1, "Channels", Float, [Port("Input", Float4, source)],
                (input, _) => $"({input[0]}).{channel.Item2}");
        Add("X", "X Component", "Channels", Float, [Port("Input", Float2, "uv")], (input, _) => $"({input[0]}).x");
        Add("Y", "Y Component", "Channels", Float, [Port("Input", Float2, "uv")], (input, _) => $"({input[0]}).y");
        Add("Z", "Z Component", "Channels", Float, [Port("Input", Float3, "float3(0.0, 0.0, 0.0)")], (input, _) => $"({input[0]}).z");
        Add("RGB", "RGB Components", "Channels", Float3, [Port("Color", Float4, source)], (input, _) => $"({input[0]}).rgb");
        Add("XY", "XY Components", "Channels", Float2, [Port("Vector", Float4, "0.0")], (input, _) => $"({input[0]}).xy");
        Add("Combine2", "Combine Vector 2", "Channels", Float2, Scalars("X", "Y"), (input, _) => $"float2({input[0]}, {input[1]})");
        Add("Combine3", "Combine Vector 3", "Channels", Float3, Scalars("X", "Y", "Z"), (input, _) => $"float3({string.Join(", ", input)})");
        Add("Combine4", "Combine Vector 4", "Channels", Float4, [Port("R", Float, "0.0"), Port("G", Float, "0.0"), Port("B", Float, "0.0"), Port("A", Float, "1.0")],
            (input, _) => $"float4({string.Join(", ", input)})");
        Add("RGBToColor", "RGB to Color", "Channels", Float4, [Port("RGB", Float3, "0.0"), Port("Alpha", Float, "1.0")],
            (input, _) => $"float4({input[0]}, {input[1]})");

        var color = Port("Color", Float4, source);
        var amount = Port("Amount", Float, "1.0");
        Add("Invert", "Invert", "Color", Float4, [color, amount],
            (input, _) => $"float4(lerp(({input[0]}).rgb, 1.0 - ({input[0]}).rgb, {input[1]}), ({input[0]}).a)");
        Add("Saturation", "Saturation", "Color", Float4, [color, amount],
            (input, _) => $"float4(lerp(Luminance(({input[0]}).rgb), ({input[0]}).rgb, {input[1]}), ({input[0]}).a)");
        Add("Luminance", "Luminance", "Color", Float, [color], (input, _) => $"Luminance(({input[0]}).rgb)");
        Add("Tint", "Tint", "Color", Float4, [color, Port("Tint", Float4, white)], (input, _) => $"{input[0]} * {input[1]}");
        Add("Brightness", "Brightness", "Color", Float4, [color, amount], (input, _) => $"float4(({input[0]}).rgb * {input[1]}, ({input[0]}).a)");
        Add("Contrast", "Contrast", "Color", Float4, [color, amount], (input, _) => $"float4((({input[0]}).rgb - 0.5) * {input[1]} + 0.5, ({input[0]}).a)");
        Add("Mix", "Lerp", "Color", Float4, [Port("A", Float4, black), Port("B", Float4, white), Port("T", Float, "0.5")],
            (input, _) => $"lerp({input[0]}, {input[1]}, {input[2]})");

        foreach (var operation in new[] { ("Add", "+"), ("Subtract", "-"), ("Multiply", "*"), ("Divide", "/") })
        {
            string defaultB = operation.Item1 is "Multiply" or "Divide" ? "1.0" : "0.0";
            Add(operation.Item1, operation.Item1, "Vector Math", Float4, [Port("A", Float4, "0.0"), Port("B", Float4, defaultB)],
                (input, _) => $"{input[0]} {operation.Item2} {input[1]}");
            Add(operation.Item1 + "Scalar", operation.Item1, "Scalar Math", Float, [Port("A", Float, "0.0"), Port("B", Float, defaultB)],
                (input, _) => $"{input[0]} {operation.Item2} {input[1]}");
        }

        foreach (var operation in new[] { ("Absolute", "abs"), ("Negate", ""), ("One Minus", ""), ("Sine", "sin"), ("Cosine", "cos"),
                     ("Fraction", "frac"), ("Floor", "floor"), ("Ceiling", "ceil"), ("Round", "round"), ("Saturate", "saturate"),
                     ("Square Root", "sqrt"), ("Exp", "exp"), ("Sign", "sign") })
        {
            string kind = operation.Item1.Replace(" ", "", StringComparison.Ordinal);
            Add(kind, operation.Item1, "Scalar Math", Float, Scalars("Input"),
                (input, _) => operation.Item1 switch
                {
                    "Negate" => $"-({input[0]})",
                    "One Minus" => $"1.0 - ({input[0]})",
                    _ => $"{operation.Item2}({input[0]})"
                });
        }
        foreach (var operation in new[] { ("Minimum", "min"), ("Maximum", "max"), ("Power", "pow"), ("Modulo", "fmod"), ("Step", "step") })
        {
            string defaultB = operation.Item1 is "Power" or "Modulo" ? "1.0" : "0.0";
            Add(operation.Item1, operation.Item1, "Scalar Math", Float, [Port("A", Float, "0.0"), Port("B", Float, defaultB)],
                (input, _) => $"{operation.Item2}({input[0]}, {input[1]})");
        }
        Add("Clamp", "Clamp", "Scalar Math", Float, [Port("Input", Float, "0.0"), Port("Min", Float, "0.0"), Port("Max", Float, "1.0")],
            (input, _) => $"clamp({input[0]}, {input[1]}, {input[2]})");
        Add("Smoothstep", "Smoothstep", "Scalar Math", Float, [Port("Min", Float, "0.0"), Port("Max", Float, "1.0"), Port("Input", Float, "0.5")],
            (input, _) => $"smoothstep({input[0]}, {input[1]}, {input[2]})");
        Add("ScalarMix", "Lerp", "Scalar Math", Float, [Port("A", Float, "0.0"), Port("B", Float, "1.0"), Port("T", Float, "0.5")],
            (input, _) => $"lerp({input[0]}, {input[1]}, {input[2]})");
        Add("If", "If Greater", "Scalar Math", Float4, [Port("A", Float, "0.0"), Port("B", Float, "0.0"), Port("True", Float4, white), Port("False", Float4, black)],
            (input, _) => $"{input[0]} > {input[1]} ? {input[2]} : {input[3]}");
        Add("Dot", "Dot Product", "Vector Math", Float, [Port("A", Float4, "0.0"), Port("B", Float4, "0.0")],
            (input, _) => $"dot({input[0]}, {input[1]})");
        Add("Cross", "Cross Product", "Vector Math", Float3, [Port("A", Float3, "float3(1.0, 0.0, 0.0)"), Port("B", Float3, "float3(0.0, 1.0, 0.0)")],
            (input, _) => $"cross({input[0]}, {input[1]})");
        Add("Normalize", "Normalize", "Vector Math", Float4, [Port("Input", Float4, white)], (input, _) => $"normalize({input[0]})");
        Add("Length", "Length", "Vector Math", Float, [Port("Input", Float4, "0.0")], (input, _) => $"length({input[0]})");
        Add("Distance", "Distance", "Vector Math", Float, [Port("A", Float4, "0.0"), Port("B", Float4, "0.0")],
            (input, _) => $"distance({input[0]}, {input[1]})");
        Add("DerivativeX", "Derivative X", "Vector Math", Float4, [Port("Input", Float4, source)], (input, _) => $"ddx({input[0]})");
        Add("DerivativeY", "Derivative Y", "Vector Math", Float4, [Port("Input", Float4, source)], (input, _) => $"ddy({input[0]})");

        AddCustom("CustomFloat", "Custom Float", Float, "0.5");
        AddCustom("CustomFloat2", "Custom Vector 2", Float2, "uv");
        AddCustom("CustomFloat3", "Custom Vector 3", Float3, source + ".rgb");
        AddCustom("CustomFloat4", "Custom Vector 4", Float4, source);

        return nodes;

        void AddCustom(string kind, string label, ShaderValueType type, string a) =>
            nodes.Add(new ShaderNodeDefinition(kind, label, "Custom HLSL", type,
                [Port("A", type, a), Port("B", type, "1.0"), Port("C", type, "0.0")], [], null, "A"));
    }

    private static ShaderPort Port(string name, ShaderValueType type, string fallback) =>
        new(name, type) { DefaultExpression = fallback };

    private static ShaderPort[] Scalars(params string[] names) =>
        names.Select(name => Port(name, Float, "0.0")).ToArray();
}
