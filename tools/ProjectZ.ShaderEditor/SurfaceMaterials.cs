using System.IO;
using System.Text;
using static ProjectZ.ShaderEditor.ShaderValueType;

namespace ProjectZ.ShaderEditor;

public sealed record SurfaceMaterialChannel(string Name, string Label, ShaderValueType Type, string ConstantKind,
    double Minimum, double Maximum, double Step, IReadOnlyList<double> Default);

/// <summary>Editable 3D material graphs shared between the material editor and surface renderers.</summary>
public static class SurfaceMaterials
{
    public static IReadOnlyList<SurfaceMaterialChannel> Channels { get; } = Array.AsReadOnly(new SurfaceMaterialChannel[]
    {
        Channel("BaseColor", "Base color", Float4, "Color", 0, 1, .01, 1, 1, 1, 1),
        Channel("Roughness", "Roughness", Float, "Float", 0, 1, .01, .38),
        Channel("Metallic", "Metallic", Float, "Float", 0, 1, .01, 0),
        Channel("Transmission", "Transmission", Float, "Float", 0, 1, .01, 0),
        Channel("IndexOfRefraction", "Index of refraction", Float, "Float", 1, 3, .001, 1.5),
        Channel("Absorption", "Absorption (RGB)", Float3, "Vector3", 0, 16, .01, 0, 0, 0),
        Channel("NormalStrength", "Normal strength", Float, "Float", 0, 4, .01, 1),
        Channel("AmbientOcclusion", "Ambient occlusion", Float, "Float", 0, 1, .01, 0.65),
        Channel("Emission", "Emission (RGB)", Float3, "Vector3", 0, 16, .01, 0, 0, 0)
    });

    public static IReadOnlyList<ShaderPreset> Presets { get; } = Array.AsReadOnly(new ShaderPreset[]
    {
        Preset("clear-water", "Clear Water", "Water", "Clear transmission with refraction and contact occlusion.",
            [.82, .96, 1.0], .055, 0, .97, 1.333, [.32, .12, .07], 1, .8),
        Preset("pool-water", "Pool Water", "Water", "Light turquoise transmission and a soft ripple.",
            [.42, .88, .91], .095, 0, .93, 1.333, [.48, .22, .13], .7, .72),
        Preset("ocean-water", "Ocean Water", "Water", "Blue-green absorption and rougher reflections.",
            [.24, .66, .74], .15, 0, .85, 1.339, [.72, .31, .19], 1.26, .74),
        Preset("murky-water", "Murky Water", "Water", "Brown sediment tint and shallow transmission.",
            [.36, .23, .10], .25, 0, .63, 1.333, [.85, 1.36, 2.75], .85, .86),
        Preset("glass", "Glass", "Glass", "Smooth transparent glass with a higher index of refraction.",
            [.96, 1.0, .97], .02, 0, 1, 1.52, [.04, .015, .028], .52, .52),
        Preset("ceramic", "Glazed Ceramic", "Ceramic", "Opaque blue ceramic with a glossy glaze.",
            [.055, .29, .54], .14, 0, 0, 1.55, [0, 0, 0], .26, .7),
        Preset("brushed-metal", "Brushed Metal", "Metal", "Opaque metal with muted reflections.",
            [.56, .59, .65], .34, 1, 0, 1.5, [0, 0, 0], .85, .62),
        Preset("matte-paint", "Matte Paint", "Paint", "Warm opaque paint with diffuse lighting and a subdued highlight.",
            [.54, .24, .15], .78, 0, 0, 1.46, [0, 0, 0], .38, .9)
    });

    public static ShaderDocument CreatePreset(string id) =>
        (Presets.FirstOrDefault(preset => preset.Id == id) ??
            throw new ArgumentException($"Unknown material preset '{id}'.", nameof(id))).CreateDocument();

    internal static ShaderPort[] CreatePorts() => Channels.Select(channel => new ShaderPort(channel.Name, channel.Type)
    {
        DefaultExpression = channel.Type == Float
            ? ShaderGenerator.FormatNumber(channel.Default[0])
            : $"{ShaderGenerator.TypeName(channel.Type)}({string.Join(", ", channel.Default.Select(ShaderGenerator.FormatNumber))})"
    }).ToArray();

    /// <summary>Returns the corresponding node if a channel has a direct editable constant.</summary>
    public static ShaderNode? FindConstant(ShaderDocument document, SurfaceMaterialChannel channel)
    {
        var output = document.Nodes.SingleOrDefault(node => node.Kind == "SurfaceOutput");
        var connection = document.Connections.Find(wire => wire.Target == output?.Id && wire.Input == channel.Name);
        return document.Nodes.Find(node => node.Id == connection?.Source && node.Kind == channel.ConstantKind);
    }

    /// <summary>Generates a material function for a renderer providing surface UVs, time, geometry and lighting.</summary>
    public static string GenerateHlsl(ShaderDocument document)
    {
        var ordered = document.GetEvaluationOrder();
        var output = ordered.SingleOrDefault(node => node.Kind == "SurfaceOutput") ??
            throw new InvalidDataException("The shader graph does not have a material output.");
        var upstream = document.Connections.ToLookup(wire => wire.Target);
        var bindings = document.Connections.ToDictionary(wire => (wire.Target, wire.Input), wire => wire.Source);
        var reachable = new HashSet<Guid> { output.Id };
        var pending = new Stack<Guid>();
        pending.Push(output.Id);
        while (pending.TryPop(out var current))
            foreach (var wire in upstream[current])
                if (reachable.Add(wire.Source))
                    pending.Push(wire.Source);

        var source = new StringBuilder();
        source.AppendLine("#define SAMPLE_SOURCE(uv) float4(1.0, 1.0, 1.0, 1.0)");
        source.AppendLine(ShaderGenerator.SharedFunctions);
        source.AppendLine("#undef SAMPLE_SOURCE");
        source.AppendLine("""
            struct SurfaceChannels
            {
                float3 BaseColor, Absorption, Emission;
                float Roughness, Metallic, Transmission, IndexOfRefraction, NormalStrength, AmbientOcclusion;
            };
            """);
        var body = new StringBuilder();
        body.AppendLine("SurfaceChannels EvaluateSurfaceMaterial(float2 uv, float Time)");
        body.AppendLine("{");
        body.AppendLine("    const float2 TexelSize = float2(1.0, 1.0) / 512.0;");
        var outputs = new Dictionary<Guid, (string Name, ShaderValueType Type)>();
        foreach (var node in ordered)
        {
            if (node.Id == output.Id || !reachable.Contains(node.Id))
                continue;
            var definition = ShaderNodes.Get(node.Kind);
            var type = definition.Output!.Value;
            string name = $"materialNode{outputs.Count + 1}";
            string[] inputs = definition.Inputs.Select(port => Resolve(node, port)).ToArray();
            string expression;
            if (definition.HasExpression)
            {
                string function = $"Evaluate_{name}";
                string hlslType = ShaderGenerator.TypeName(type);
                ShaderGenerator.ValidateCustomExpression(node.Expression, definition.Label);
                source.AppendLine($"{hlslType} {function}({hlslType} A, {hlslType} B, {hlslType} C, float2 uv, float Time, float2 TexelSize)");
                source.AppendLine("{");
                source.AppendLine($"    return ({node.Expression});");
                source.AppendLine("}").AppendLine();
                expression = $"{function}({string.Join(", ", inputs)}, uv, Time, TexelSize)";
            }
            else
            {
                expression = node.Kind == "PixelPosition" ? "(uv * 512.0)" :
                    definition.Expression!(inputs, node.Values.Select(ShaderGenerator.FormatNumber).ToArray());
            }
            body.AppendLine($"    {ShaderGenerator.TypeName(type)} {name} = {expression};");
            outputs.Add(node.Id, (name, type));
        }
        body.AppendLine("    SurfaceChannels result;");
        var ports = ShaderNodes.Get("SurfaceOutput").Inputs;
        for (int i = 0; i < Channels.Count; i++)
        {
            var channel = Channels[i];
            string value = Resolve(output, ports[i]);
            if (channel.Type == Float4)
                value = $"({value}).rgb";
            string minimum = ShaderGenerator.FormatNumber(channel.Minimum);
            string maximum = ShaderGenerator.FormatNumber(channel.Maximum);
            body.AppendLine($"    result.{channel.Name} = clamp({value}, {minimum}, {maximum});");
        }
        body.AppendLine("    return result;");
        body.AppendLine("}");
        return source.Append(body).ToString();

        string Resolve(ShaderNode node, ShaderPort port)
        {
            if (!bindings.TryGetValue((node.Id, port.Name), out var boundId))
                return port.DefaultExpression;
            var bound = outputs[boundId];
            return bound.Type == port.Type ? bound.Name : $"({ShaderGenerator.TypeName(port.Type)})({bound.Name})";
        }
    }

    internal static string GeneratePreviewEffect(ShaderDocument document) =>
        ShaderGenerator.XamlHeader + "\n#undef SAMPLE_SOURCE\n" + GenerateHlsl(document) + """

        float4 ProjectZShader(SpriteInput input) : PS_TARGET
        {
            float2 uv = input.TexCoord;
            float2 sphereUv = (uv - 0.5) * float2(2.65, 2.25);
            float radius = dot(sphereUv, sphereUv);
            float cell = CheckerFactor(uv * float2(12.0, 10.0));
            float3 background = lerp(float3(0.055, 0.075, 0.1), float3(0.20, 0.25, 0.31), cell);
            if (radius >= 1.0) return float4(background, 1.0) * input.Color;
            SurfaceChannels m = EvaluateSurfaceMaterial(uv, Time);
            float3 normal = normalize(float3(sphereUv * m.NormalStrength, sqrt(1.0 - radius)));
            float3 light = normalize(float3(-0.55, -0.45, 0.77));
            float3 halfVector = normalize(light + float3(0.0, 0.0, 1.0));
            float diffuse = max(dot(light, normal), 0.0) * 0.75;
            float f0 = (m.IndexOfRefraction - 1.0) / (m.IndexOfRefraction + 1.0);
            float fresnel = f0 * f0 + (1.0 - f0 * f0) * pow(1.0 - normal.z, 5.0);
            float highlight = pow(max(0.0, dot(normal, halfVector)), lerp(140.0, 5.0, m.Roughness));
            float3 transmittedRay = refract(float3(0.0, 0.0, -1.0), normal, 1.0 / m.IndexOfRefraction);
            float refractedCell = CheckerFactor((uv + transmittedRay.xy * 0.3) * float2(12.0, 10.0));
            float3 transmitted = lerp(float3(0.07, 0.1, 0.15), float3(0.3, 0.4, 0.5), refractedCell);
            transmitted *= exp(-m.Absorption * normal.z) * m.BaseColor;
            float ambient = 0.23 - 0.1 * saturate(sphereUv.y + 0.2) * m.AmbientOcclusion;
            float3 opaque = m.BaseColor * (ambient + diffuse * (1.0 - m.Metallic));
            float3 reflection = lerp(float3(1.0, 1.0, 1.0), m.BaseColor, m.Metallic) * (highlight + fresnel * 0.4);
            float3 color = lerp(opaque, transmitted * (1.0 - fresnel), m.Transmission * (1.0 - m.Metallic)) + reflection + m.Emission;
            return float4(pow(max(color, 0.0), 1.0 / 2.2), 1.0) * input.Color;
        }
        technique XamlEffect
        {
            pass P0 { PixelShader = compile PS_MODEL ProjectZShader(); }
        }
        """;

    private static SurfaceMaterialChannel Channel(string name, string label, ShaderValueType type, string kind,
        double minimum, double maximum, double step, params double[] values) =>
        new(name, label, type, kind, minimum, maximum, step, Array.AsReadOnly(values));

    private static ShaderPreset Preset(string id, string name, string category, string description, double[] color,
        double roughness, double metallic, double transmission, double ior, double[] absorption, double normals, double ao) =>
        new(id, name, category, description, () =>
        {
            var output = ShaderNodes.Create("SurfaceOutput", 440, 220);
            var document = new ShaderDocument { Name = name, Nodes = [output] };
            double[][] values = [[..color, 1], [roughness], [metallic], [transmission], [ior], absorption,
                [normals], [ao], [0, 0, 0]];
            for (int i = 0; i < Channels.Count; i++)
            {
                var channel = Channels[i];
                var node = ShaderNodes.Create(channel.ConstantKind, 32 + i / 5 * 190, 22 + i % 5 * 123);
                node.Name = channel.Label;
                node.Values = (double[])values[i].Clone();
                document.Nodes.Add(node);
                document.Connections.Add(new ShaderConnection { Source = node.Id, Target = output.Id, Input = channel.Name });
            }
            return document;
        });
}
