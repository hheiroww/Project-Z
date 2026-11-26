namespace ProjectZ.ShaderEditor;

public static partial class ShaderPresets
{
    /// <summary>Editable screen shaders authored for a real-time audio host.</summary>
    public static IReadOnlyList<ShaderPreset> Realtime { get; } = Array.AsReadOnly<ShaderPreset>(
    [
        Recipe("spectral-rings", "Spectral rings", "Audio reactive",
            "Layered rings breathe with bass, split into cool spectral bands, and bloom on each beat.",
            "float4(lerp(A.rgb, B.rgb * (0.55 + 0.45 * cos(38.0 * length(uv - 0.5) + float3(0.1, 2.1, 4.2) - Time * C.g)), " +
            "saturate(C.r * (0.25 + AudioLevel) * pow(saturate(0.5 + 0.5 * cos(72.0 * length(uv - 0.5) - 4.0 * AudioLow - " +
            "Time * C.g - BeatPhase * 6.2831853)), 7.0) * (1.0 - smoothstep(0.30, 0.69, length(uv - 0.5))))), " +
            "max(A.a, (1.0 - smoothstep(0.26, 0.66, length(uv - 0.5))) * saturate((0.42 + 0.5 * AudioBeat + 0.12 * Macro01) * " +
            "pow(saturate(0.5 + 0.5 * cos(72.0 * length(uv - 0.5) - 4.0 * AudioLow - Time * C.g - BeatPhase * 6.2831853)), 5.0))))",
            "Ring tint", [0.55, 0.93, 1, 1], "Ring gain", 1.15, "Phase speed", 1.25),
        Recipe("prism-bloom", "Prism bloom", "Audio reactive",
            "Mid and high bands separate the color channels around a glowing six-point prism.",
            "float4(lerp(A.rgb, saturate(B.rgb + 0.35 * cos(14.0 * length(uv - 0.5) + float3(0.0, 2.1, 4.2) + Time * C.g)), " +
            "saturate(C.r * (0.3 + AudioMid + 0.3 * AudioHigh) * (1.0 - smoothstep(0.03, 0.65, length(uv - 0.5))))), " +
            "max(A.a, (1.0 - smoothstep(0.06, 0.65, length(uv - 0.5))) * " +
            "saturate((0.43 + 0.65 * AudioHigh) * pow(saturate(0.5 + 0.5 * cos(6.0 * atan2(uv.y - 0.5, uv.x - 0.5) + " +
            "Time * C.g + 4.0 * AudioMid + Macro02)), 2.0))))",
            "Prismatic tint", [0.62, 0.35, 0.94, 1], "Bloom gain", 1.34, "Rotation speed", 0.65),
        Recipe("folded-pulse", "Folded pulse", "Audio reactive",
            "Folded neon facets pulse with the beat and reveal contrasting colors in the quiet.",
            "float4(lerp(A.rgb, saturate(B.rgb * (0.50 + 0.5 * cos(float3(0.0, 2.1, 4.2) + " +
            "Time * C.g + AudioLow * 3.2 + length(uv - 0.5) * 23.0))), " +
            "saturate(C.r * (0.25 + 0.6 * AudioBeat + 0.65 * AudioLevel))), " +
            "max(A.a, (1.0 - smoothstep(0.18, 0.68, length(uv - 0.5))) * " +
            "pow(saturate(1.0 - 3.8 * abs(0.5 - frac(9.0 * abs(uv.x - uv.y) + 5.0 * abs(uv.x + uv.y - 1.0) - " +
            "BeatPhase * (0.7 + Macro03)))), 2.0)))",
            "Facet tint", [1, 0.43, 0.83, 1], "Pulse gain", 1.18, "Drift speed", 0.62)
    ]);
}
