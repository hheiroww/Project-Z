namespace ProjectZ.ShaderEditor;

public static partial class ShaderPresets
{
    private static ShaderPreset[] ImageEffects() =>
    [
        Recipe("bleach-bypass", "Bleach Bypass", "Color and light", "Desaturates the image and adds film-style contrast.",
            "float4(lerp(A.rgb, saturate((lerp(A.rgb, Luminance(A.rgb), saturate(C.x)) - .5) * C.y + .5), B.a), A.a)",
            "Effect mix", [1, 1, 1, .8], "Desaturation", .65, "Contrast", 1.45),
        Recipe("duotone-ink", "Duotone Ink", "Color and light", "Maps shadows and highlights to a colored ink and warm paper.",
            "float4(lerp(A.rgb, lerp(B.rgb, float3(1, .9, .68), pow(saturate(Luminance(A.rgb)), max(.05, C.y))), saturate(C.x)), A.a)",
            "Shadow ink", [.07, .1, .23, 1], "Mix", 1, "Tone curve", .8),
        Recipe("neon-edges", "Neon Edges", "Color and light", "Lights up texture edges with adjustable spread and glow color.",
            "float4(A.rgb * .24 + B.rgb * saturate(length(SampleSource(uv + TexelSize * C.y).rgb - SampleSource(uv - TexelSize * C.y).rgb) * C.x), A.a)",
            "Neon color", [.22, .9, 1, 1], "Gain", 3, "Spread in pixels", 2),
        Recipe("soft-bloom", "Soft Bloom", "Color and light", "Spreads bright details into a soft glow.",
            "float4(A.rgb + B.rgb * max(0, (Luminance(SampleSource(uv + TexelSize * float2(C.y, 0)).rgb) + Luminance(SampleSource(uv - TexelSize * float2(C.y, 0)).rgb) + Luminance(SampleSource(uv + TexelSize * float2(0, C.y)).rgb) + Luminance(SampleSource(uv - TexelSize * float2(0, C.y)).rgb)) * .25 - .48) * C.x, A.a)",
            "Glow tint", [1, .84, .66, 1], "Strength", 1.3, "Radius in pixels", 5),
        Recipe("iris-vignette", "Iris Vignette", "Color and light", "Shades the corners with a soft elliptical falloff.",
            "float4(lerp(A.rgb, B.rgb, smoothstep(saturate(C.y) * .65, 1.0, length((uv - .5) * float2(1.35, 1.7))) * saturate(C.x)), A.a)",
            "Shade color", [.035, .012, .07, 1], "Strength", .82, "Clear center", .7),
        Recipe("thermal-vision", "Thermal Vision", "Color and light", "Maps image brightness into cool shadows and hot highlights.",
            "float4(lerp(A.rgb, saturate(float3(1.5 * Luminance(A.rgb), 3 * Luminance(A.rgb) - 1.1, 1 - 2 * Luminance(A.rgb)) + B.rgb * C.y), saturate(C.x)), A.a)",
            "Palette bias", [.1, 0, .12, 1], "Mix", 1, "Palette shift", .35),
        Recipe("prism-split", "Prism Split", "Distortion", "Pulls red and blue outward from the optical center.",
            "float4(lerp(A.rgb, float3(SampleSource(saturate(uv + (uv - .5) * C.y)).r, A.g, SampleSource(saturate(uv - (uv - .5) * C.y)).b) * B.rgb, saturate(C.x)), A.a)",
            "Channel balance", [1, 1, 1, 1], "Mix", 1, "Separation", .17),
        Recipe("pixel-blocks", "Pixel Blocks", "Distortion", "Resamples the image into a grid of crisp square blocks.",
            "float4(lerp(A.rgb, SampleSource((floor(uv * max(1, C.y)) + .5) / max(1, C.y)).rgb * B.rgb, saturate(C.x)), A.a)",
            "Pixel tint", [1, 1, 1, 1], "Mix", 1, "Blocks per axis", 35),
        Recipe("glass-lens", "Glass Lens", "Distortion", "Magnifies the center under a rounded glass lens.",
            "float4(lerp(A.rgb, SampleSource(saturate(.5 + (uv - .5) / max(.1, C.y))).rgb * B.rgb, (1 - smoothstep(.28, .32, length(uv - .5))) * saturate(C.x)), A.a)",
            "Lens tint", [.92, .98, 1, 1], "Mix", 1, "Magnification", 1.65),
        Recipe("tape-jitter", "Tape Jitter", "Distortion", "Offsets horizontal bands with intermittent animated noise.",
            "float4(lerp(A.rgb, SampleSource(saturate(uv + float2((NoiseHash(float2(floor(uv.y * 20), floor(Time * C.y))) - .5) * C.x, 0))).rgb * B.rgb, step(.72, NoiseHash(float2(floor(uv.y * 20), floor(Time * C.y) + 34)))), A.a)",
            "Shifted band tint", [.98, .9, 1, 1], "Horizontal drift", .22, "Changes per second", 14),
        Recipe("halftone-dots", "Halftone Dots", "Print and screen", "Draws an ink dot pattern with size driven by brightness.",
            "float4(lerp(A.rgb, lerp(B.rgb, float3(1, .94, .79), smoothstep(.02, .07, length(frac(uv * C.y) - .5) - (.48 - .43 * saturate(Luminance(A.rgb))))), saturate(C.x)), A.a)",
            "Ink color", [.04, .025, .07, 1], "Mix", .85, "Dots per axis", 55),
        Recipe("crt-scanlines", "CRT Scanlines", "Print and screen", "Adds moving scanlines and a phosphor tint.",
            "float4(lerp(A.rgb, A.rgb * B.rgb * (.72 + .28 * sin(uv.y * 540 + Time * C.y)), saturate(C.x)), A.a)",
            "Phosphor tint", [.72, 1, .83, 1], "Scanline depth", .85, "Roll speed", 10),
    ];
}
