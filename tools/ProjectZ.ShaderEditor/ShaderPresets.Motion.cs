namespace ProjectZ.ShaderEditor;

public static partial class ShaderPresets
{
    private static ShaderPreset[] MotionEffects() =>
    [
        ShutterRecipe("directional-shutter", "Directional Shutter Blur",
            "Sweeps the current image along a pixel velocity vector during the exposure.",
            "X pixels per frame · Y pixels per frame · shutter degrees", [36, 8, 180, 0],
            "A.xy * TexelSize * saturate(A.z / 360)"),
        ShutterRecipe("zoom-shutter", "Zoom Shutter Blur",
            "Samples a zoom path about an adjustable optical center.",
            "Center X · center Y · zoom per frame · shutter degrees", [.5, .5, .19, 180],
            "(uv - A.xy) * A.z * saturate(A.w / 360)"),
        ShutterRecipe("orbital-shutter", "Orbital Shutter Blur",
            "Samples the image along an angular path with a screen-space center.",
            "Center X · center Y · angle per frame · shutter degrees", [.5, .5, 18, 180],
            "float2(-(uv.y - A.y) * TexelSize.x / TexelSize.y, (uv.x - A.x) * TexelSize.y / TexelSize.x) * radians(A.z) * saturate(A.w / 360)"),
        Recipe("kick-twitch", "Kick Twitch", "Motion",
            "A brief shake on each beat. Set beats per minute to match the soundtrack.",
            "SampleSource(saturate(0.5 + (uv - 0.5) * (1 - .7 * C.x * pow(1 - frac(Time * C.y / 60), 14)) + " +
            "float2(sin(Time * 70), cos(Time * 54)) * C.x * pow(1 - frac(Time * C.y / 60), 14)))",
            "Light balance", [1, 1, 1, 1], "Shake amount", .024, "Beats per minute", 94),
    ];

    private static ShaderPreset ShutterRecipe(string id, string name, string description,
        string settingsLabel, double[] defaults, string velocity)
    {
        return new ShaderPreset(id, name, "Motion", description, () =>
        {
            var settings = ShaderNodes.Create("Vector4", 30, 90);
            settings.Name = settingsLabel;
            settings.Values = (double[])defaults.Clone();
            var vector = ShaderNodes.Create("CustomFloat4", 235, 90);
            vector.Name = "Shutter offset";
            vector.Expression = $"float4({velocity}, 0, 0)";
            var samples = ShaderNodes.Create("CustomFloat4", 460, 90);
            samples.Name = "Uniform shutter samples";
            samples.Expression = "(" + string.Join(" + ", Enumerable.Range(0, 7).Select(i =>
                $"SampleSource(saturate(uv - A.xy * ({i}.0 / 6.0)))")) + ") / 7.0";
            var output = ShaderNodes.Create("Output", 690, 90);
            return new ShaderDocument
            {
                Name = name,
                Mode = ShaderMode.Advanced,
                Nodes = [settings, vector, samples, output],
                Connections = [Connect(settings, vector, "A"), Connect(vector, samples, "A"), Connect(samples, output, "Color")]
            };
        });
    }
}
