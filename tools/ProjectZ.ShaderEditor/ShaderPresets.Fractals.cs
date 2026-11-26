namespace ProjectZ.ShaderEditor;

public static partial class ShaderPresets
{
    private static ShaderPreset[] Fractals() =>
    [
        FractalRecipe("ifs-frost-inlay", "IFS Frost Inlay", "Six nested angular folds with a blue edge and bright frost filaments.",
            [.49, 2.16, .85, .18], [.26, .58, .13, .32], [.022, .066, .17, 1], [.38, .88, 1, 1],
            "abs(A.xy) - float2(B.x, B.x * .77)",
            "abs(abs(C.x) - abs(C.y) * .57)"),
        FractalRecipe("ifs-amber-branch", "IFS Amber Branch", "A branching fold with warm veins and copper highlights.",
            [.54, 2.85, 1.34, -.14], [.35, .87, .3, .52], [.19, .029, .006, 1], [1, .64, .23, 1],
            "float2(abs(A.x), A.y) - float2(B.x * .81, B.x * .49)",
            "min(abs(C.x), abs(C.y - .6 * abs(C.x)))"),
        FractalRecipe("ifs-prism-reef", "IFS Prism Reef", "A rotating cell fold with cyan ridges and a violet interior.",
            [.57, 2.24, .53, .26], [.31, 1.19, .23, .48], [.15, .022, .23, 1], [.29, 1, .86, 1],
            "abs(abs(A.xy) - float2(B.x * 1.07, B.x * .8)) - B.x * .54",
            "abs(length(C.xy * float2(1, .61)) - .31)"),
    ];

    private static ShaderPreset FractalRecipe(string id, string name, string description,
        double[] shape, double[] style, double[] low, double[] high, string fold, string ridge)
    {
        return new ShaderPreset(id, name, "Fractals", description, () =>
        {
            var geometry = FractalInput("Vector4", "Contraction · Zoom · Rotation · Drift", shape, 30, 40);
            var lighting = FractalInput("Vector4", "Filament width · Exposure · Halo · Opacity", style, 30, 150);
            var baseColor = FractalInput("Color", "Shadow", low, 30, 260);
            var peakColor = FractalInput("Color", "Highlight", high, 30, 370);
            var plane = ShaderNodes.Create("CustomFloat4", 245, 40);
            plane.Name = "Aspect-correct coordinates";
            plane.Expression = "float4((uv - .5) * float2(TexelSize.y / max(TexelSize.x, .00001), 1) * A.y, 0, 0)";
            var nodes = new List<ShaderNode> { geometry, lighting, baseColor, peakColor, plane };
            var wires = new List<ShaderConnection> { Connect(geometry, plane, "A") };
            var current = plane;
            for (var level = 0; level < 6; level++)
            {
                var branch = ShaderNodes.Create("CustomFloat4", 450 + level * 190, 40);
                branch.Name = $"Affine fold {level + 1}";
                branch.Expression = $"float4({fold}, A.z, A.w)";
                nodes.Add(branch);
                wires.Add(Connect(current, branch, "A"));
                wires.Add(Connect(geometry, branch, "B"));
                var turn = ShaderNodes.Create("CustomFloat4", 450 + level * 190, 200);
                turn.Name = "Rotate and contract";
                const string angle = "(B.z + .34 * sin(Time * B.w))";
                turn.Expression =
                    $"float4((A.x * cos({angle}) - A.y * sin({angle})) * (1.7 - saturate(B.x)), " +
                    $"(A.x * sin({angle}) + A.y * cos({angle})) * (1.7 - saturate(B.x)), " +
                    "min(A.z == 0 ? 10 : A.z, length(A.xy)), A.w + exp(-7 * dot(A.xy, A.xy)))";
                nodes.Add(turn);
                wires.Add(Connect(branch, turn, "A"));
                wires.Add(Connect(geometry, turn, "B"));
                current = turn;
            }

            var coverage = ShaderNodes.Create("CustomFloat4", 1600, 70);
            coverage.Name = "Filament, halo and exposure";
            string filaments = ridge.Replace("C.", "B.", StringComparison.Ordinal);
            coverage.Expression = $"float4(exp(-({filaments}) / max(.007, A.x * .13)), " +
                "saturate(B.w / 4) * max(0, A.z), max(0, A.y), max(0, A.w))";
            var finish = ShaderNodes.Create("CustomFloat4", 1810, 120);
            finish.Name = "Filament lighting and transparency";
            finish.Expression = "float4(lerp(A.rgb, B.rgb, saturate(C.x + C.y)) * C.z, saturate((C.x + C.y) * C.w))";
            var output = ShaderNodes.Create("Output", 2020, 120);
            nodes.AddRange([coverage, finish, output]);
            wires.AddRange([Connect(lighting, coverage, "A"), Connect(current, coverage, "B"),
                Connect(baseColor, finish, "A"), Connect(peakColor, finish, "B"), Connect(coverage, finish, "C"),
                Connect(finish, output, "Color")]);
            return new ShaderDocument { Name = name, Mode = ShaderMode.Advanced, Nodes = nodes, Connections = wires };
        });
    }

    private static ShaderNode FractalInput(string type, string name, double[] values, double x, double y)
    {
        var node = ShaderNodes.Create(type, x, y);
        node.Name = name;
        node.Values = (double[])values.Clone();
        return node;
    }
}
