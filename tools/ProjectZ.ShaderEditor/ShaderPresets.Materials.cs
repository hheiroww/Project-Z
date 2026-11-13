namespace ProjectZ.ShaderEditor;

public static partial class ShaderPresets
{
    private static ShaderPreset[] Materials() =>
    [
        Recipe("brushed-steel", "Brushed Steel", "Specular",
            "Fine directional brushing with a moving metal highlight.",
            """
            float4(lerp(A.rgb, B.rgb * (0.32 + 0.36 * ValueNoise(float2(uv.x * 5.0, uv.y * 148.0)) +
                0.60 * pow(saturate(1.0 - abs(uv.x + uv.y * 0.23 - (0.50 + 0.33 * sin(Time * C.y))) * 1.5), 6.0)),
                saturate(C.x)), A.a)
            """,
            "Steel tint", [0.69, 0.75, 0.82, 1.0], "Metal amount", 0.70, "Sweep speed", 0.60),
        Recipe("gloss-coat", "Gloss Coat", "Specular",
            "A soft moving highlight gives a picture a polished finish.",
            """
            float4(A.rgb + B.rgb * C.x * pow(saturate(1.0 - length((uv -
                float2(0.49 + 0.30 * sin(Time * C.y), 0.32)) * float2(1.0, 2.5))), 7.0), A.a)
            """,
            "Gloss color", [0.94, 0.96, 1.0, 1.0], "Highlight amount", 0.75, "Sweep speed", 0.65),
        Recipe("pearl-sheen", "Pearl Sheen", "Specular",
            "Iridescent bands drift across a soft pearl tint.",
            """
            float4(lerp(A.rgb, 0.55 * A.rgb + B.rgb * (0.30 + 0.19 * cos(
                (uv.x * 6.0 + uv.y * 8.0 + Time * C.y) + float3(0.0, 2.094, 4.189))),
                saturate(C.x)), A.a)
            """,
            "Pearl tint", [0.94, 0.83, 1.0, 1.0], "Sheen amount", 0.65, "Drift speed", 0.50),
        Recipe("edge-glints", "Edge Glints", "Specular",
            "Highlights local contrast edges with a gentle shimmer.",
            """
            float4(A.rgb + B.rgb * C.x * (0.70 + 0.30 * sin(uv.x * 21.0 + uv.y * 12.0 + Time * C.y)) *
                smoothstep(0.02, 0.23, length(SampleSource(uv + TexelSize).rgb -
                    SampleSource(uv - TexelSize).rgb)), A.a)
            """,
            "Glint color", [0.74, 0.88, 1.0, 1.0], "Glint strength", 0.85, "Shimmer speed", 2.0),
        Recipe("glass-highlight", "Glass Highlight", "Specular",
            "A narrow diagonal reflection passes over the source like a pane of glass.",
            """
            float4(A.rgb + B.rgb * C.x * (1.0 - smoothstep(0.015, 0.13,
                abs(uv.x + uv.y * 0.42 - (0.72 + 0.46 * sin(Time * C.y))))), A.a)
            """,
            "Light color", [0.85, 0.96, 1.0, 1.0], "Highlight amount", 0.55, "Sweep speed", 0.75),
        Recipe("mirror-flip", "Mirror Flip", "Reflection",
            "Mirrors the left side across an adjustable vertical axis.",
            """
            float4(lerp(A.rgb, SampleSource(float2(saturate(saturate(C.y) - abs(uv.x - saturate(C.y))), uv.y)).rgb * B.rgb,
                saturate(C.x)), A.a)
            """,
            "Mirror tint", [1.0, 1.0, 1.0, 1.0], "Mirror amount", 1.0, "Mirror axis", 0.50),
        Recipe("wet-floor", "Wet Floor", "Reflection",
            "Reflects the upper image below a horizon with a soft fade.",
            """
            float4(lerp(A.rgb, B.rgb * SampleSource(float2(saturate(uv.x +
                sin(uv.y * 48.0 + Time * 2.0) * 0.007), saturate(2.0 * clamp(C.y, 0.02, 0.94) - uv.y))).rgb,
                saturate(C.x) * smoothstep(clamp(C.y, 0.02, 0.94), clamp(C.y, 0.02, 0.94) + 0.025, uv.y) *
                (1.0 - smoothstep(clamp(C.y, 0.02, 0.94) + 0.025, 1.0, uv.y))), A.a)
            """,
            "Floor tint", [0.72, 0.86, 0.99, 1.0], "Reflection amount", 0.86, "Horizon", 0.58),
        Recipe("puddle-mirror", "Puddle Mirror", "Reflection",
            "An irregular puddle reveals a rippling reflection of the picture.",
            """
            float4(lerp(A.rgb, B.rgb * SampleSource(float2(saturate(uv.x +
                0.016 * sin(uv.y * 40.0 + Time * C.y)), saturate(1.29 - uv.y))).rgb,
                saturate(C.x) * (1.0 - smoothstep(0.33, 0.49,
                    length((uv - float2(0.5, 0.70)) * float2(1.0, 2.6)) +
                    0.10 * ValueNoise(uv * 19.0)))), A.a)
            """,
            "Puddle tint", [0.62, 0.78, 0.92, 1.0], "Reflection amount", 0.94, "Ripple speed", 1.5),
        Recipe("chrome-sweep", "Chrome Sweep", "Reflection",
            "Curved mirror bands turn the source into a moving chrome reflection.",
            """
            float4(lerp(A.rgb, B.rgb * (0.25 + 0.93 * SampleSource(float2(
                frac(1.0 - uv.x + 0.22 * sin(uv.y * 8.0 + Time * C.y)),
                saturate(0.15 + 0.70 * uv.y))).rgb) *
                (0.45 + 0.62 * pow(abs(sin(uv.y * 13.0 + uv.x * 3.0)), 5.0)), saturate(C.x)), A.a)
            """,
            "Chrome tint", [0.76, 0.87, 0.98, 1.0], "Metal amount", 0.86, "Sweep speed", 0.75),
        Recipe("frosted-glass", "Frosted Glass", "Reflection",
            "A grain-distorted reflection softens the source like textured glass.",
            """
            float4(lerp(A.rgb, B.rgb * (0.18 + 0.42 * SampleSource(
                uv + (float2(ValueNoise(uv * 119.0), ValueNoise(uv * 119.0 + 17.0)) - 0.5) * C.y).rgb +
                0.40 * SampleSource(uv - (float2(ValueNoise(uv * 119.0), ValueNoise(uv * 119.0 + 17.0)) -
                    0.5) * C.y).rgb), saturate(C.x)), A.a)
            """,
            "Glass tint", [0.79, 0.90, 0.99, 1.0], "Glass amount", 0.92, "Distortion", 0.035),
        Recipe("surface-ripples", "Surface Ripples", "Water",
            "Crossing wave fronts refract the picture with a water tint.",
            """
            float4(lerp(A.rgb, B.rgb * SampleSource(saturate(uv +
                float2(sin(uv.y * 37.0 - Time * C.y), cos(uv.x * 29.0 + Time * C.y)) * 0.019)).rgb,
                saturate(C.x)), A.a)
            """,
            "Water tint", [0.68, 0.92, 1.0, 1.0], "Ripple amount", 0.84, "Wave speed", 2.4),
        Recipe("shallow-caustics", "Shallow Caustics", "Water",
            "Bright cellular light patterns move over a clear water tint.",
            """
            float4(A.rgb * lerp(1.0, B.rgb, saturate(C.x) * 0.50) + B.rgb * C.x * 0.55 *
                pow(saturate(1.0 - abs(VoronoiFactor(uv * 12.0 + float2(Time * C.y, -Time * C.y * 0.63)) -
                    VoronoiFactor(uv * 13.0 - float2(Time * C.y * 0.40, Time * C.y * 0.77))) * 3.0), 7.0), A.a)
            """,
            "Caustic light", [0.34, 0.88, 0.99, 1.0], "Light strength", 0.72, "Flow speed", 0.65),
        Recipe("ocean-waves", "Ocean Waves", "Water",
            "Broad rolling waves shade deep water with bright crest foam.",
            """
            float4(lerp(A.rgb, B.rgb * (0.28 + 0.64 * (0.5 + 0.5 * sin(
                (uv.y + uv.y * uv.y * 0.6) * 26.0 + 1.4 * sin(uv.x * 9.0 + Time * C.y) - Time * C.y))) +
                float3(0.59, 0.79, 0.80) * pow(saturate(sin((uv.y + uv.y * uv.y * 0.6) * 26.0 +
                    1.4 * sin(uv.x * 9.0 + Time * C.y) - Time * C.y)), 20.0) *
                (0.38 + 0.34 * ValueNoise(uv * float2(44.0, 22.0) - float2(0.0, Time * C.y))),
                saturate(C.x)), A.a)
            """,
            "Ocean tint", [0.035, 0.46, 0.67, 1.0], "Water amount", 0.83, "Swell speed", 1.55),
        Recipe("rain-rings", "Rain Rings", "Water",
            "Fading rings spread from staggered points on the surface.",
            """
            float4(A.rgb + B.rgb * C.x *
                (1.0 - smoothstep(0.018, 0.13, abs(VoronoiFactor(uv * 10.0) -
                    0.57 * frac(Time * C.y + ValueNoise(floor(uv * 10.0)))))) *
                (1.0 - frac(Time * C.y + ValueNoise(floor(uv * 10.0)))), A.a)
            """,
            "Ring light", [0.45, 0.82, 0.99, 1.0], "Ring brightness", 0.46, "Rain speed", 0.75),
        Recipe("waterfall", "Waterfall", "Water",
            "Downward foam streaks and finer spray cover the source.",
            """
            float4(lerp(A.rgb, B.rgb * (0.19 + 0.58 * pow(ValueNoise(
                float2(uv.x * 38.0, uv.y * 6.0 - Time * C.y)), 0.65)) +
                0.38 * smoothstep(0.53, 0.94, ValueNoise(float2(uv.x * 150.0, uv.y * 46.0 -
                    Time * C.y * 5.0))), saturate(C.x)), A.a)
            """,
            "Water tint", [0.54, 0.86, 1.0, 1.0], "Water amount", 0.83, "Fall speed", 2.3),
        Recipe("sand-dunes", "Sand Dunes", "Terrain",
            "Wind-shaped sand ridges with a fine grain layer.",
            """
            float4(lerp(A.rgb, B.rgb * (0.48 + 0.50 * pow(
                0.50 + 0.50 * sin((uv.y + uv.x * 0.29) * C.y + 2.0 * ValueNoise(uv * 5.0)), 0.65) +
                0.07 * ValueNoise(uv * 133.0)), saturate(C.x)), A.a)
            """,
            "Sand color", [0.86, 0.58, 0.31, 1.0], "Terrain amount", 0.86, "Ridge frequency", 38),
        Recipe("stratified-rock", "Stratified Rock", "Terrain",
            "Layered sediment bands combine with stone grain.",
            """
            float4(lerp(A.rgb, B.rgb * (0.35 + 0.32 * smoothstep(0.15, 0.32,
                frac(uv.y * C.y + 1.6 * ValueNoise(uv * 6.0) + 0.45 * ValueNoise(uv * 21.0))) +
                0.29 * ValueNoise(uv * 107.0)), saturate(C.x)), A.a)
            """,
            "Stone color", [0.56, 0.46, 0.38, 1.0], "Terrain amount", 0.88, "Layer frequency", 24),
        Recipe("alpine-snow", "Alpine Snow", "Terrain",
            "A patchy snow line reveals dark crags under the high ground.",
            """
            float4(lerp(A.rgb, lerp(float3(0.14, 0.19, 0.24) * (0.40 + 0.62 * ValueNoise(uv * 9.0) +
                0.43 * ValueNoise(uv * float2(26.0, 49.0))),
                B.rgb * (0.76 + 0.19 * ValueNoise(uv * 29.0)),
                smoothstep(C.y, C.y + 0.18, 1.0 - uv.y + 0.44 * ValueNoise(uv * 7.0) -
                    0.18 * ValueNoise(uv * 43.0))), saturate(C.x)), A.a)
            """,
            "Snow color", [0.82, 0.91, 0.99, 1.0], "Terrain amount", 0.94, "Snow line", 0.53),
        Recipe("lava-cracks", "Lava Cracks", "Terrain",
            "Incandescent fissures pulse between dark volcanic plates.",
            """
            float4(lerp(A.rgb, float3(0.018, 0.009, 0.008) + 0.037 * ValueNoise(uv * 70.0) +
                B.rgb * pow(saturate(1.0 - abs(sin((0.7 * ValueNoise(uv * 8.0) +
                    0.3 * ValueNoise(uv * 19.0)) * 28.0)) * 2.0), 3.5) *
                (0.62 + 0.36 * sin(Time * C.y + uv.x * 9.0 + uv.y * 6.0)), saturate(C.x)), A.a)
            """,
            "Lava color", [1.0, 0.27, 0.025, 1.0], "Terrain amount", 0.91, "Pulse speed", 1.1),
        Recipe("topographic-lines", "Topographic Lines", "Terrain",
            "Contour lines trace the height of a procedural landscape.",
            """
            float4(lerp(A.rgb, B.rgb * (0.15 + 0.48 * ValueNoise(uv * 5.0)) +
                B.rgb * 0.55 * (1.0 - smoothstep(0.03, 0.12,
                    abs(frac((ValueNoise(uv * 5.0) + 0.24 * ValueNoise(uv * 13.0)) * C.y) - 0.5))),
                saturate(C.x)), A.a)
            """,
            "Map color", [0.27, 0.80, 0.62, 1.0], "Terrain amount", 0.84, "Contour density", 16),
        Recipe("wind-in-grass", "Wind in Grass", "Grass",
            "Thin grass strands bend in traveling wind.",
            """
            float4(lerp(A.rgb, B.rgb * (0.28 + 0.50 * ValueNoise(uv * 9.0) +
                0.28 * pow(0.5 + 0.5 * sin((uv.x + uv.y * 0.18 +
                    0.035 * sin(uv.y * 14.0 + Time * C.y)) * 220.0), 7.0)), saturate(C.x)), A.a)
            """,
            "Grass color", [0.25, 0.68, 0.17, 1.0], "Grass amount", 0.86, "Wind speed", 1.3),
        Recipe("meadow-blades", "Meadow Blades", "Grass",
            "Individual blade streaks sit in irregular meadow patches.",
            """
            float4(lerp(A.rgb, B.rgb * (0.22 + 0.42 * ValueNoise(uv * 11.0) +
                0.40 * smoothstep(0.36, 0.70, ValueNoise(float2(
                    (uv.x + uv.y * 0.27 + 0.019 * sin(uv.y * 9.0)) * C.y, uv.y * 17.0)))),
                saturate(C.x)), A.a)
            """,
            "Meadow color", [0.38, 0.78, 0.28, 1.0], "Grass amount", 0.84, "Blade density", 125),
        Recipe("turf-rows", "Turf Rows", "Grass",
            "Alternating mown rows give turf a visible direction.",
            """
            float4(lerp(A.rgb, B.rgb * (0.47 + 0.25 * smoothstep(0.31, 0.69,
                frac((uv.x + uv.y * 0.4) * C.y)) + 0.28 * ValueNoise(float2(uv.x * 140.0, uv.y * 100.0))),
                saturate(C.x)), A.a)
            """,
            "Turf color", [0.15, 0.58, 0.22, 1.0], "Grass amount", 0.85, "Row count", 8),
        Recipe("dry-prairie", "Dry Prairie", "Grass",
            "Golden stalks sway above a muted dry-grass base.",
            """
            float4(lerp(A.rgb, B.rgb * (0.28 + 0.35 * ValueNoise(uv * 12.0) +
                0.42 * smoothstep(0.62, 0.91, ValueNoise(float2((uv.x + 0.39 * uv.y +
                    0.018 * sin(uv.y * 17.0 - Time * C.y)) * 95.0, uv.y * 8.0)))), saturate(C.x)), A.a)
            """,
            "Prairie color", [0.82, 0.58, 0.23, 1.0], "Grass amount", 0.85, "Breeze speed", 0.90),
        Recipe("dewy-grass", "Dewy Grass", "Grass",
            "Small highlights glint across a dark field of grass blades.",
            """
            float4(lerp(A.rgb, B.rgb * (0.24 + 0.52 * ValueNoise(float2((uv.x + uv.y * 0.31) * 123.0,
                uv.y * 14.0))) + float3(0.66, 0.89, 0.98) *
                pow(1.0 - VoronoiFactor(uv * 38.0), 18.0) *
                (0.55 + 0.45 * sin(Time * C.y + uv.x * 42.0)), saturate(C.x)), A.a)
            """,
            "Grass color", [0.15, 0.54, 0.32, 1.0], "Grass amount", 0.88, "Glint speed", 1.1)
    ];
}
