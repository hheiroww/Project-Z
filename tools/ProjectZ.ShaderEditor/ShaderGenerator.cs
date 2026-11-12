using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace ProjectZ.ShaderEditor;

public static class ShaderGenerator
{
    public static string Generate(ShaderDocument document)
    {
        var ordered = document.GetEvaluationOrder();
        var output = ordered.Single(node => node.Kind == "Output");
        var upstream = document.Connections.ToLookup(wire => wire.Target);
        var bindings = document.Connections.ToDictionary(wire => (wire.Target, wire.Input), wire => wire.Source);

        var reachable = new HashSet<Guid> { output.Id };
        var pending = new Stack<Guid>();
        pending.Push(output.Id);
        while (pending.TryPop(out var current))
        {
            foreach (var wire in upstream[current])
                if (reachable.Add(wire.Source))
                    pending.Push(wire.Source);
        }

        var source = new StringBuilder(Header);
        source.AppendLine().AppendLine();
        var body = new StringBuilder();
        body.AppendLine("float4 ProjectZShader(SpriteInput input) : PS_TARGET");
        body.AppendLine("{");
        body.AppendLine("    float2 uv = input.TexCoord;");

        var outputs = new Dictionary<Guid, (string Name, ShaderValueType Type)>();
        foreach (var node in ordered)
        {
            if (!reachable.Contains(node.Id) || node.Id == output.Id)
                continue;
            var definition = ShaderNodes.Get(node.Kind);
            var type = definition.Output!.Value;
            string name = $"node{outputs.Count + 1}";
            string[] inputs = definition.Inputs.Select(port => Resolve(node, port)).ToArray();
            string expression;
            if (definition.HasExpression)
            {
                string function = $"Evaluate_{name}";
                string hlslType = TypeName(type);
                ValidateCustomExpression(node.Expression, definition.Label);
                source.AppendLine($"{hlslType} {function}({hlslType} A, {hlslType} B, {hlslType} C, float2 uv)");
                source.AppendLine("{");
                source.AppendLine($"    return ({node.Expression});");
                source.AppendLine("}").AppendLine();
                expression = $"{function}({string.Join(", ", inputs)}, uv)";
            }
            else
            {
                expression = definition.Expression!(inputs, node.Values.Select(FormatNumber).ToArray());
            }
            body.AppendLine($"    {TypeName(type)} {name} = {expression}; // {definition.Label}");
            outputs.Add(node.Id, (name, type));
        }

        var outputDefinition = ShaderNodes.Get("Output");
        body.AppendLine($"    float4 color = {Resolve(output, outputDefinition.Inputs[0])};");
        string alpha = bindings.ContainsKey((output.Id, "Alpha")) ? Resolve(output, outputDefinition.Inputs[1]) : "color.a";
        body.AppendLine($"    float alpha = saturate({alpha});");
        body.AppendLine("    return float4(color.rgb * alpha, alpha) * input.Color;");
        body.AppendLine("}");
        body.AppendLine();
        body.AppendLine("technique XamlEffect");
        body.AppendLine("{");
        body.AppendLine("    pass P0 { PixelShader = compile PS_MODEL ProjectZShader(); }");
        body.AppendLine("}");
        return source.Append(body).ToString();

        string Resolve(ShaderNode node, ShaderPort port)
        {
            if (bindings.TryGetValue((node.Id, port.Name), out var sourceId))
            {
                var bound = outputs[sourceId];
                return bound.Type == port.Type ? bound.Name : $"({TypeName(port.Type)})({bound.Name})";
            }
            return port.Type == ShaderValueType.Float ? port.DefaultExpression : $"({TypeName(port.Type)})({port.DefaultExpression})";
        }
    }

    private static void ValidateCustomExpression(string expression, string label)
    {
        if (string.IsNullOrWhiteSpace(expression))
            throw new InvalidDataException($"{label} needs an HLSL expression.");
        // Limit the snippet to expression tokens, including comparisons and the ternary operator.
        // Keep preprocessing, comments and statements outside the generated helper.
        if (Regex.IsMatch(expression, @"[^A-Za-z0-9_.,()+*/%<>!&|^~?:=\[\]\- \t\r\n]") ||
            Regex.IsMatch(expression, @"//|/\*|\*/|<%|%>|%:|\?\?"))
            throw new InvalidDataException($"{label} accepts an HLSL expression. Remove comments, statements, or preprocessor directives.");
    }

    private static string TypeName(ShaderValueType type) => type switch
    {
        ShaderValueType.Float => "float",
        ShaderValueType.Float2 => "float2",
        ShaderValueType.Float3 => "float3",
        ShaderValueType.Float4 => "float4",
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    private static string FormatNumber(double value)
    {
        string text = ((float)value).ToString("R", CultureInfo.InvariantCulture);
        return text.Contains('.') || text.Contains('E') ? text : text + ".0";
    }

    private const string Header = """
        // Generated by the Project Z Shader Editor.
        // Build with MonoGame MGFXC for DirectX_12 (DIRECTX12) or DirectX_11.
        #if DIRECTX12
        #define PS_MODEL ps_6_0
        #define PS_TARGET SV_Target0
        Texture2D SourceTexture : register(t0);
        SamplerState SourceSampler : register(s0);
        #define SAMPLE_SOURCE(uv) SourceTexture.Sample(SourceSampler, uv)
        #else
        #define PS_MODEL ps_4_0
        #define PS_TARGET COLOR0
        sampler2D SourceTexture : register(s0);
        #define SAMPLE_SOURCE(uv) tex2D(SourceTexture, uv)
        #endif

        float Time;
        float2 TexelSize;
        struct SpriteInput
        {
            float4 Position : SV_POSITION;
            float4 Color : COLOR0;
            float2 TexCoord : TEXCOORD0;
        };

        float4 SampleSource(float2 uv)
        {
            float4 color = SAMPLE_SOURCE(uv);
            return float4(color.rgb / max(color.a, 0.000001), color.a);
        }

        float Luminance(float3 color)
        {
            return dot(color, float3(0.2126, 0.7152, 0.0722));
        }

        float2 TransformUV(float2 uv, float2 scale, float2 offset, float turns)
        {
            float2 centered = (uv - 0.5) * scale;
            float sine, cosine;
            sincos(turns * 6.283185307, sine, cosine);
            return float2(dot(centered, float2(cosine, -sine)), dot(centered, float2(sine, cosine))) + 0.5 + offset;
        }

        float2 PolarUV(float2 uv)
        {
            float2 centered = uv - 0.5;
            return float2(atan2(centered.y, centered.x) * 0.159154943 + 0.5, length(centered) * 2.0);
        }

        float GradientFactor(float2 uv, float turns)
        {
            float sine, cosine;
            sincos(turns * 6.283185307, sine, cosine);
            return saturate(dot(uv - 0.5, float2(cosine, sine)) + 0.5);
        }

        float CheckerFactor(float2 uv)
        {
            float2 cell = floor(uv);
            return frac((cell.x + cell.y) * 0.5) * 2.0;
        }

        float NoiseHash(float2 samplePosition)
        {
            return frac(sin(dot(samplePosition, float2(127.1, 311.7))) * 43758.5453);
        }

        float ValueNoise(float2 uv)
        {
            float2 cell = floor(uv);
            float2 f = frac(uv);
            f = f * f * (3.0 - 2.0 * f);
            return lerp(lerp(NoiseHash(cell), NoiseHash(cell + float2(1.0, 0.0)), f.x),
                        lerp(NoiseHash(cell + float2(0.0, 1.0)), NoiseHash(cell + 1.0), f.x), f.y);
        }

        float VoronoiFactor(float2 uv)
        {
            float2 cell = floor(uv);
            float2 f = frac(uv);
            float distanceSquared = 2.0;
            [unroll] for (int y = -1; y <= 1; y++)
            {
                [unroll] for (int x = -1; x <= 1; x++)
                {
                    float2 offset = float2(x, y);
                    float2 neighbor = cell + offset;
                    float2 delta = offset + float2(NoiseHash(neighbor), NoiseHash(neighbor + 19.37)) - f;
                    distanceSquared = min(distanceSquared, dot(delta, delta));
                }
            }
            return saturate(sqrt(distanceSquared));
        }

        float3 BlendRgb(float3 baseColor, float3 layer, float mode)
        {
            if (mode < 0.5) return layer;
            if (mode < 1.5) return saturate(baseColor + layer);
            if (mode < 2.5) return baseColor * layer;
            if (mode < 3.5) return 1.0 - (1.0 - baseColor) * (1.0 - layer);
            if (mode < 4.5)
                return lerp(2.0 * baseColor * layer, 1.0 - 2.0 * (1.0 - baseColor) * (1.0 - layer), step(0.5, baseColor));
            return saturate(baseColor - layer);
        }

        float4 BlendLayer(float4 baseColor, float4 layer, float opacity, float mode)
        {
            float baseAlpha = saturate(baseColor.a);
            float layerAlpha = saturate(layer.a) * saturate(opacity);
            float alpha = layerAlpha + baseAlpha * (1.0 - layerAlpha);
            float3 layerColor = lerp(layer.rgb, BlendRgb(baseColor.rgb, layer.rgb, mode), baseAlpha);
            float3 color = layerColor * layerAlpha + baseColor.rgb * baseAlpha * (1.0 - layerAlpha);
            return float4(color / max(alpha, 0.000001), alpha);
        }
        """;
}
